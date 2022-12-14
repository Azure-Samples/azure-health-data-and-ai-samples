using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using UploadFhirJson.Configuration;
using UploadFhirJson.FhirClient;
using UploadFhirJson.Model;

namespace UploadFhirJson.ProcessFhir
{
    public class ProcessFhirJson : IProcessFhirJson
    {
        public ProcessFhirJson(BlobConfiguration blobConfiguration, IFhirClient fhirClient, BlobServiceClient blobServiceClient, TelemetryClient telemetryClient = null, ILogger<ProcessFhirJson> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _fhirClient = fhirClient;
            _blobConfiguration = blobConfiguration;
            _blobServiceClient = blobServiceClient;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;
        public string Id => _id;
        public string Name => "ProcessFhirJson";
        public bool isFilesSkipped = false;

        public async Task<HttpResponseData> Execute(HttpRequestData httpRequestData)
        {
            DateTime start = DateTime.Now;
            try
            {
                FhirInput fhirInputs = JsonConvert.DeserializeObject<FhirInput>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                if (fhirInputs != null && fhirInputs.sortedHL7files.Count > 0)
                {
                    var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FhirJsonContainer);
                    FhirResponse fhirReponse = new();
                    foreach (var item in fhirInputs.sortedHL7files)
                    {
                        BlobClient blobClient = blobContainer.GetBlobClient(item.HL7FileName);
                        if (blobClient != null && await blobClient.ExistsAsync())
                        {
                            FhirDetails fhirDetails = new();
                            var blobData = await blobClient.OpenReadAsync();
                            using (var streamReader = new StreamReader(blobData))
                            {
                                fhirDetails = JsonConvert.DeserializeObject<FhirDetails>(await streamReader.ReadToEndAsync());
                            }
                            if (isFilesSkipped)
                            {
                                fhirReponse.skipped.Add(await UploadSkippedFile(fhirDetails));
                            }
                            else
                            {
                                var result = await ProcessFhirRequest(fhirDetails, fhirInputs.proceedOnError);
                                if (result != null && result.StatusCode == 200)
                                {
                                    fhirReponse.success.Add(result);
                                }
                                else
                                { fhirReponse.failed.Add(result); }
                            }

                            await blobContainer.DeleteBlobAsync(item.HL7FileName);
                        }
                    }
                    var response = httpRequestData.CreateResponse(HttpStatusCode.OK);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fhirReponse)));
                    return await Task.FromResult(response);
                }
                else
                {
                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    var response = httpRequestData.CreateResponse(HttpStatusCode.NoContent);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"No content found in incoming request."));
                    return await Task.FromResult(response);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} Error while sending Fhir data to server.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Error while sending Fhir data to server :{(ex.InnerException != null ? ex.InnerException : ex.Message)}"));
                return await Task.FromResult(response);
            }
        }

        public async Task<Response> ProcessFhirRequest(FhirDetails fhirInput, bool proceedOnError)
        {
            Response fhirReponse = new();
            HttpResponseMessage httpResponseMessage = new();
            if (fhirInput != null)
            {
                if (!string.IsNullOrEmpty(fhirInput.HL7FileName) && fhirInput.HL7Conversion)
                {
                    _logger?.LogInformation($"Processing {fhirInput.HL7FileName} file to fhir server.");
                    string requestBody = System.Text.Encoding.Default.GetString(Convert.FromBase64String(fhirInput.FhirJson));
                    httpResponseMessage = await _fhirClient.Send(requestBody);
                    var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        fhirReponse.StatusCode = (int)HttpStatusCode.OK;
                        fhirReponse.FileName = fhirInput.HL7FileName;
                        await UploadToSuccessBlob(fhirInput.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.ProcessedBlobContainer);
                    }
                    else
                    {
                        isFilesSkipped = !proceedOnError ? true : false;
                        fhirReponse.StatusCode = (int)httpResponseMessage.StatusCode;
                        fhirReponse.FileName = fhirInput.HL7FileName;
                        fhirReponse.Error = responseString;
                        await UploadToFailBlob(fhirInput.HL7FileName, requestBody, _blobConfiguration.ConvertedContainer, _blobConfiguration.HL7FailedBlob);
                    }

                }
                else
                {
                    fhirReponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    fhirReponse.FileName = fhirInput.HL7FileName;
                    fhirReponse.Error = $"Not posting the file to fhir server as requested content is blank.";
                }
            }
            return fhirReponse;
        }

        public async Task<Response> UploadSkippedFile(FhirDetails fhirInput)
        {
            await UploadToSuccessBlob(fhirInput.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.SkippedBlobContainer);
            Response fhirReponse = new();
            fhirReponse.FileName = fhirInput.HL7FileName;
            fhirReponse.StatusCode = (int)HttpStatusCode.FailedDependency;
            fhirReponse.Error = $"File skipped.";
            return fhirReponse;
        }

        public async Task UploadToSuccessBlob(string fileName, string soruceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            await sourceBlobClient.DeleteAsync();
        }

        public async Task UploadToFailBlob(string fileName, string fileData, string soruceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FailedBlobContainer);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(targetBloblName + "/" + fileName);
            await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            if (!string.IsNullOrEmpty(fileData))
            {
                var blobClient = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FailedBlobContainer);
                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                await blobClient.UploadBlobAsync(_blobConfiguration.FhirFailedBlob + "/" + Path.GetFileNameWithoutExtension(fileName) + ".json", memoryStream);
                memoryStream.Close();
                await memoryStream.DisposeAsync();
            }
            await sourceBlobClient.DeleteAsync();
        }
    }
}