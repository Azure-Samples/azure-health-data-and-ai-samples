﻿using Azure.Core;
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
        public ProcessFhirJson(BlobConfiguration blobConfiguration, AppConfiguration appConfiguration, IFhirClient fhirClient, TelemetryClient telemetryClient = null, ILogger<ProcessFhirJson> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _fhirClient = fhirClient;
            _blobConfiguration = blobConfiguration;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly IFhirClient _fhirClient;
        private readonly ILogger _logger;
        private readonly AppConfiguration _appConfiguration;
        public string Id => _id;
        public string Name => "ProcessFhirJson";
        public bool isFilesSkipped = false;
        //int _minRetry = 1;
        //int _maxRetry = 3;


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
                FhirResponse fhirResponse = new();
                ProcessRequest request = JsonConvert.DeserializeObject<ProcessRequest>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                var requestBody = System.Text.Encoding.Default.GetString(Convert.FromBase64String(request.FileList));
                FhirInput fhirInputs = JsonConvert.DeserializeObject<FhirInput>(requestBody);
                if (fhirInputs != null && fhirInputs.sortedHL7files.Count > 0)
                {
                    if (request.FileProcessInSequence)
                    {
                        _logger?.LogInformation($"Porcess data in sequence start at {start.ToString("yyyyMMdd HH:mm:ss")}");
                        fhirResponse = await ProcessDataInSequence(fhirInputs, request);
                        _logger?.LogInformation($"Porcess data in sequence end at {DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    }
                    else
                    {
                        _logger?.LogInformation($"Porcess data in batch start at {start}");
                        fhirResponse = await ProcessDataInBatch(fhirInputs, request);
                        _logger?.LogInformation($"Porcess data in batch start at {DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    }
                    var response = httpRequestData.CreateResponse(HttpStatusCode.OK);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(fhirResponse)));
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


        private async Task<FhirResponse> ProcessDataInSequence(FhirInput fhirInput, ProcessRequest request)
        {

            BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.FhirJsonContainer);
            //var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FhirJsonContainer);
            FhirResponse fhirReponse = new();
            var take = request.BatchLimit;
            var skip = (request.CurrentIndex * request.BatchLimit);
            int i = 0;
            foreach (var item in fhirInput.sortedHL7files.Skip(skip).Take(take))
            {
                // _minRetry = 1;
                var hl7JsonFile = Path.GetFileNameWithoutExtension(item.HL7FileName) + ".json";
                BlobClient blobClient = blobContainer.GetBlobClient(hl7JsonFile);
                if (blobClient != null && await blobClient.ExistsAsync())
                {
                    FhirDetails fhirDetails = new();
                    var blobData = await blobClient.OpenReadAsync();

                    if (request.isFilesSkipped || isFilesSkipped)
                    {
                        var list = fhirInput.sortedHL7files.SkipWhile(obj => obj.HL7FileName == item.HL7FileName).Skip(i).ToList();
                        UploadSkippedFile(list, ref fhirReponse);
                        return await Task.FromResult(fhirReponse);
                    }
                    else
                    {
                        using (var streamReader = new StreamReader(blobData))
                        {
                            fhirDetails = JsonConvert.DeserializeObject<FhirDetails>(await streamReader.ReadToEndAsync());
                        }
                        if (fhirDetails.HL7Conversion)
                        {
                            var result = await ProcessFhirRequest(fhirDetails, fhirInput.proceedOnError);
                            if (result != null && result.StatusCode != 200)
                            {
                                fhirReponse.response.Add(result);
                            }
                            fhirReponse.IsFileSkipped = isFilesSkipped;
                        }
                        i++;
                    }
                }
            }

            return await Task.FromResult(fhirReponse);
        }

        private async Task<FhirResponse> ProcessDataInBatch(FhirInput fhirInput, ProcessRequest request)
        {

            BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.FhirJsonContainer);
            //var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FhirJsonContainer);
            FhirResponse fhirReponse = new();
            var take = request.BatchLimit;
            var skip = (request.CurrentIndex * request.BatchLimit);
            //int i = 0;
            ParallelOptions parallelOptions = new();
            parallelOptions.MaxDegreeOfParallelism = request.BatchLimit;
            var fhirList = fhirInput.sortedHL7files.Skip(skip).Take(take);
            await Parallel.ForEachAsync(fhirList, parallelOptions, async (hl7FileName, CancellationToken) =>
            {
                var hl7JsonFile = Path.GetFileNameWithoutExtension(hl7FileName.HL7FileName) + ".json";
                BlobClient blobClient = blobContainer.GetBlobClient(hl7JsonFile);
                if (blobClient != null && await blobClient.ExistsAsync())
                {
                    FhirDetails fhirDetails = new();
                    var blobData = await blobClient.OpenReadAsync();

                    using (var streamReader = new StreamReader(blobData))
                    {
                        fhirDetails = JsonConvert.DeserializeObject<FhirDetails>(await streamReader.ReadToEndAsync());
                    }
                    if (fhirDetails.HL7Conversion)
                    {
                        var result = await ProcessFhirRequest(fhirDetails, fhirInput.proceedOnError);
                        if (result != null && result.StatusCode != 200)
                        {
                            fhirReponse.response.Add(result);
                        }
                        fhirReponse.IsFileSkipped = isFilesSkipped;
                    }
                }
            });

            return await Task.FromResult(fhirReponse);
        }

        /// <summary>
        /// Processed the fhir json data to fhir server.
        /// </summary>
        /// <param name="fhirInput"></param>
        /// <param name="proceedOnError"></param>
        /// <returns>Response</returns>
        private async Task<Response> ProcessFhirRequest(FhirDetails fhirInput, bool proceedOnError)
        {
            DateTime start = DateTime.Now;
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
                        try
                        {
                            httpResponseMessage = await _fhirClient.Send(requestBody, fhirInput.HL7FileName);

                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation($"Error while sending bundle to fhir server with exception:{ex.Message}");
                            _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                        }

                        var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                        _logger?.LogInformation($"Received response from fhir server.");

                        if (httpResponseMessage.IsSuccessStatusCode)
                        {
                            _logger?.LogInformation($"Fhir server response {(int)httpResponseMessage.StatusCode}");
                            fhirResponse.StatusCode = (int)httpResponseMessage.StatusCode;
                            fhirResponse.FileName = fhirInput.HL7FileName;
                            UploadToSuccessBlob(fhirInput.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.ProcessedBlobContainer);
                        }
                        else
                        {
                            _logger?.LogInformation($"File : {fhirInput.HL7FileName},  Fhir server response {(int)httpResponseMessage.StatusCode}, error : {responseString}");
                            isFilesSkipped = !proceedOnError ? true : false;
                            fhirResponse.StatusCode = (int)httpResponseMessage.StatusCode;
                            fhirResponse.FileName = fhirInput.HL7FileName;
                            fhirResponse.Error = responseString;
                            UploadToFailBlob(fhirInput.HL7FileName, requestBody, _blobConfiguration.ConvertedContainer, _blobConfiguration.HL7FailedBlob);

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
                UploadToSuccessBlob(item.HL7FileName, _blobConfiguration.ConvertedContainer, _blobConfiguration.SkippedBlobContainer);
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
        private void UploadToSuccessBlob(string fileName, string soruceBlobName, string targetBloblName)
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
                if (sourceBlobClient != null && sourceBlobClient.Exists())
                {
                    var copy = targetBlobClient.StartCopyFromUri(sourceBlobClient.Uri);
                    copy.WaitForCompletion();
                    sourceBlobClient.Delete();
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

        private void UploadToFailBlob(string fileName, string fileData, string soruceBlobName, string targetBloblName)
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
                if (sourceBlobClient != null && sourceBlobClient.Exists())
                {
                    var copy = targetBlobClient.StartCopyFromUri(sourceBlobClient.Uri);
                    copy.WaitForCompletion();
                    if (!string.IsNullOrEmpty(fileData))
                    {
                        _logger?.LogInformation($"UploadToFailBlob :Reading Memory Stream data to {fileName} ");
                        var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(fileData));
                        targetClient.UploadBlob(_blobConfiguration.FhirFailedBlob + "/" + Path.GetFileNameWithoutExtension(fileName) + ".json", memoryStream);
                        memoryStream.Close();
                        memoryStream.Dispose();
                        _logger?.LogInformation($"UploadToFailBlob :Uploaded Memory Stream data to {fileName} ");
                    }
                    sourceBlobClient.Delete();
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