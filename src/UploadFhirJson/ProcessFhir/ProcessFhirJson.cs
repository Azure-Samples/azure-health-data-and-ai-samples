using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using UploadFhirJson.Configuration;
using UploadFhirJson.FhirClient;
using UploadFhirJson.Model;

namespace UploadFhirJson.ProcessFhir
{
    public class ProcessFhirJson : IProcessFhirJson
    {
        public ProcessFhirJson(BlobConfiguration blobConfiguration, IFhirClient fhirClient, TelemetryClient telemetryClient = null, ILogger<ProcessFhirJson> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _fhirClient = fhirClient;
            _blobConfiguration = blobConfiguration;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;
        public string Id => _id;
        public string Name => "ProcessFhirJson";
        public bool isFilesSkipped = false;

        /// <summary>
        /// This Method will read the incoming request body, parse it and processed the data to fhir server.
        /// Based On current iteration of logic app  do until action, it will take and skip the files.
        /// If processed on error is false and any error coming from fhir respose, store all the remaning file in skipped folder.
        /// </summary>
        /// <param name="httpRequestData"></param>
        /// <returns>HttpResponseData</returns>
        public async Task<HttpResponseData> Execute(HttpRequestData httpRequestData)
        {
            DateTime start = DateTime.Now;
            try
            {
                Request request = JsonConvert.DeserializeObject<Request>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                var requestBody = System.Text.Encoding.Default.GetString(Convert.FromBase64String(request.FileList));
                FhirInput fhirInputs = JsonConvert.DeserializeObject<FhirInput>(requestBody);
                if (fhirInputs != null && fhirInputs.sortedHL7files.Count > 0)
                {
                    BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.FhirJsonContainer);
                    //var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FhirJsonContainer);
                    FhirResponse fhirReponse = new();
                    var take = request.BatchLimit;
                    var skip = (request.CurrentIndex * request.BatchLimit);
                    int i = 0;
                    foreach (var item in fhirInputs.sortedHL7files.Skip(skip).Take(take))
                    {
                        var hl7JsonFile = Path.GetFileNameWithoutExtension(item.HL7FileName) + ".json";
                        BlobClient blobClient = blobContainer.GetBlobClient(hl7JsonFile);
                        if (blobClient != null && await blobClient.ExistsAsync())
                        {
                            FhirDetails fhirDetails = new();
                            var blobData = await blobClient.OpenReadAsync();

                            if (request.isFilesSkipped || isFilesSkipped)
                            {
                                var list = fhirInputs.sortedHL7files.SkipWhile(obj => obj.HL7FileName == item.HL7FileName).Skip(i).ToList();
                                UploadSkippedFile(list, ref fhirReponse);

                                var skippedFileResponse = httpRequestData.CreateResponse(HttpStatusCode.OK);
                                skippedFileResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fhirReponse)));
                                return await Task.FromResult(skippedFileResponse);
                            }
                            else
                            {
                                using (var streamReader = new StreamReader(blobData))
                                {
                                    fhirDetails = JsonConvert.DeserializeObject<FhirDetails>(await streamReader.ReadToEndAsync());
                                }
                                if (fhirDetails.HL7Conversion)
                                {
                                    var result = await ProcessFhirRequest(fhirDetails, fhirInputs.proceedOnError);
                                    if (result != null && result.StatusCode != 200)
                                    {
                                        fhirReponse.response.Add(result);
                                    }
                                    fhirReponse.IsFileSkipped = isFilesSkipped;
                                }
                                i++;
                            }
                            //await blobContainer.DeleteBlobAsync(hl7JsonFile);
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
                _logger?.LogInformation($"Exception:  {(ex.InnerException != null ? ex.InnerException : ex.Message)}");
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Error while sending Fhir data to server :{(ex.InnerException != null ? ex.InnerException : ex.Message)}"));
                return await Task.FromResult(response);
            }
        }

        /// <summary>
        /// Processed the fhir json data to fhir server.
        /// </summary>
        /// <param name="fhirInput"></param>
        /// <param name="proceedOnError"></param>
        /// <returns>Response</returns>
        private async Task<Response> ProcessFhirRequest(FhirDetails fhirInput, bool proceedOnError)
        {
            Response fhirResponse = new();
            HttpResponseMessage httpResponseMessage = new();
            if (fhirInput != null)
            {
                if (!string.IsNullOrEmpty(fhirInput.HL7FileName))
                {
                    _logger?.LogInformation($"Processing {fhirInput.HL7FileName} file to fhir server.");
                    string requestBody = System.Text.Encoding.Default.GetString(Convert.FromBase64String(fhirInput.FhirJson));
                    _logger?.LogInformation($"sending fhir data to fhir server.");
                    if (!string.IsNullOrEmpty(requestBody))
                    {
                        httpResponseMessage = await _fhirClient.Send(requestBody);
                        var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                        _logger?.LogInformation($"Received response from fhir server.");
                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            _logger?.LogInformation($"Fhir server response {(int)httpResponseMessage.StatusCode}");
                            fhirResponse.StatusCode = (int)httpResponseMessage.StatusCode;
                            fhirResponse.FileName = fhirInput.HL7FileName;
                            await UploadToSuccessBlob(fhirInput.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.ProcessedBlobContainer);
                        }
                        else
                        {
                            _logger?.LogInformation($"File : {fhirInput.HL7FileName},  Fhir server response {(int)httpResponseMessage.StatusCode}, error : {responseString}");
                            isFilesSkipped = !proceedOnError ? true : false;
                            fhirResponse.StatusCode = (int)httpResponseMessage.StatusCode;
                            fhirResponse.FileName = fhirInput.HL7FileName;
                            fhirResponse.Error = responseString;
                            await UploadToFailBlob(fhirInput.HL7FileName, requestBody, _blobConfiguration.ConvertedContainer, _blobConfiguration.HL7FailedBlob);

                        }
                    }
                    else
                    {
                        fhirResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        fhirResponse.FileName = fhirInput.HL7FileName;
                        fhirResponse.Error = $"Not posting the file to fhir server as requested content is blank.";
                        _logger?.LogInformation($"Not posting the {fhirInput.HL7FileName} to fhir server as requested fhir json is blank.");
                    }
                }
                else
                {
                    fhirResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    fhirResponse.FileName = fhirInput.HL7FileName;
                    fhirResponse.Error = $"Not posting the file to fhir server as requested content is blank.";
                    _logger?.LogInformation($"Not posting the fhir content to fhir server as requested content is blank.");
                }
            }
            return fhirResponse;
        }

        /// <summary>
        /// Upload the remaning HL7 files to Skipped Container.
        /// </summary>
        /// <param name="fileList"></param>
        /// <param name="fhirResponse"></param>
        private void UploadSkippedFile(List<FileName> fileList, ref FhirResponse fhirResponse)
        {
            foreach (var item in fileList)
            {
                _ = UploadToSuccessBlob(item.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.SkippedBlobContainer);
                Response response = new();
                response.FileName = item.HL7FileName;
                response.StatusCode = (int)HttpStatusCode.FailedDependency;
                response.Error = $"File skipped.";
                fhirResponse.response.Add(response);
            }
        }

        /// <summary>
        /// Upload the successed HL7 files to Processed Container.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="soruceBlobName"></param>
        /// <param name="targetBloblName"></param>
        /// <returns></returns>
        private async Task UploadToSuccessBlob(string fileName, string soruceBlobName, string targetBloblName)
        {
            _logger?.LogInformation($"UploadToSuccessBlob :Start uploading {fileName} ");
            BlobContainerClient sourceClient = new(_blobConfiguration.BlobConnectionString, soruceBlobName);
            BlobContainerClient targetClient = new(_blobConfiguration.BlobConnectionString, targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);

            //var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            //var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            //BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            //BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            try
            {
                if (sourceBlobClient != null && await sourceBlobClient.ExistsAsync())
                {
                    var copy = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                    await copy.WaitForCompletionAsync();
                    await sourceBlobClient.DeleteAsync();
                    _logger?.LogInformation($"UploadToSuccessBlob : Blob uploaded to {targetBloblName} and deleted from {soruceBlobName} ");
                }
                else
                {
                    _logger?.LogInformation($"UploadToSuccessBlob : Blob {fileName} does not exists.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"UploadToSuccessBlob :Error while copying the {fileName} from {soruceBlobName} to {targetBloblName} ");
                _logger?.LogError(ex, "{Name}-{Id} Error while copying the file from {soruceBlobName} to {targetBloblName}", Name, Id, soruceBlobName, targetBloblName);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds));
            }
        }

        /// <summary>
        /// Upload the failed HL7 files to Failed Container.
        /// Failed HL7 file stored to Failed Blob and parsed fhir json files to Fhir Blob.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileData"></param>
        /// <param name="soruceBlobName"></param>
        /// <param name="targetBloblName"></param>
        /// <returns></returns>      

        private async Task UploadToFailBlob(string fileName, string fileData, string soruceBlobName, string targetBloblName)
        {
            BlobContainerClient sourceClient = new(_blobConfiguration.BlobConnectionString, soruceBlobName);
            BlobContainerClient targetClient = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.FailedBlobContainer);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(targetBloblName + "/" + fileName);

            //var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            //var targetClient = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FailedBlobContainer);
            //BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            //BlobClient targetBlobClient = targetClient.GetBlobClient(targetBloblName + "/" + fileName);

            try
            {
                if (sourceBlobClient != null && await sourceBlobClient.ExistsAsync())
                {
                    var copy = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                    await copy.WaitForCompletionAsync();
                    if (!string.IsNullOrEmpty(fileData))
                    {
                        _logger?.LogInformation($"UploadToFailBlob :Reading Memory Stream data to {fileName} ");
                        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                        await targetClient.UploadBlobAsync(_blobConfiguration.FhirFailedBlob + "/" + Path.GetFileNameWithoutExtension(fileName) + ".json", memoryStream);
                        memoryStream.Close();
                        await memoryStream.DisposeAsync();
                        _logger?.LogInformation($"UploadToFailBlob :Uploaded Memory Stream data to {fileName} ");
                    }
                    await sourceBlobClient.DeleteAsync();
                    _logger?.LogInformation($"UploadToFailBlob : Blob uploaded to {targetBloblName} and deleted from {soruceBlobName} ");
                }
                else
                {
                    _logger?.LogInformation($"UploadToFailBlob :Blob {fileName} does not exists ");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"UploadToFailBlob :Error while copying the {fileName} from {soruceBlobName} to {targetBloblName} ");
                _logger?.LogError(ex, "{Name}-{Id} Error while copying the file from {soruceBlobName} to {targetBloblName}", Name, Id, soruceBlobName, targetBloblName);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks).TotalMilliseconds));
            }
        }
    }
}