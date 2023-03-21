using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using UploadFhirJson.Configuration;
using UploadFhirJson.FhirOperation;
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
        private readonly AppConfiguration _appConfiguration;
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
        public async Task<string> Execute(string requestData)
        {
            DateTime start = DateTime.Now;
            try
            {
                FhirResponse fhirResponse = new();
                string hl7FilesArray = string.Empty;
                FhirInput fhirInputs = new();
                ProcessRequest request = JsonConvert.DeserializeObject<ProcessRequest>(requestData);
                var hl7ArrayFileName = System.Text.Encoding.Default.GetString(Convert.FromBase64String(request.Hl7ArrayFileName));

                if (request != null && !string.IsNullOrEmpty(hl7ArrayFileName))
                {
                    BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, _blobConfiguration.ValidatedContainer);

                    var blobFile = blobContainer.GetBlobClient(hl7ArrayFileName);

                    if (blobFile != null && await blobFile.ExistsAsync())
                    {
                        var blobFileData = await blobFile.OpenReadAsync();
                        using (var streamReader = new StreamReader(blobFileData))
                        {
                            hl7FilesArray = await streamReader.ReadToEndAsync();
                        }

                        blobFileData.Close();
                    }
                }

                if (!string.IsNullOrEmpty(hl7FilesArray))
                {
                    List<FileName> Hl7fileList = JsonConvert.DeserializeObject<List<FileName>>(hl7FilesArray);

                    if(Hl7fileList.Any())
                    {
                        fhirInputs.sortedHL7files = Hl7fileList;
                    }
                    
                }

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
                    return await Task.FromResult(JsonConvert.SerializeObject(fhirResponse));
                }
                else
                {
                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    return await Task.FromResult($"No content found in incoming request.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} Error while sending Fhir data to server.", Name, Id);
                _logger?.LogInformation($"Exception:  {(ex.InnerException != null ? ex.InnerException : ex.Message)}");
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                return await Task.FromResult($"Error while sending Fhir data to server :{(ex.InnerException != null ? ex.InnerException : ex.Message)}");
            }
        }

        private async Task<FhirResponse> ProcessDataInSequence(FhirInput fhirInput, ProcessRequest request)
        {

            BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, !request.SkipFhirPostProcess ? _blobConfiguration.HL7FhirPostPorcessJson : _blobConfiguration.FhirJsonContainer);
            //var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.FhirJsonContainer);
            FhirResponse fhirReponse = new();
            int i = 0;
            int maxDegreeOfParallelism = _appConfiguration.UploadFhirJsonMaxParallelism;
            int totalFileCount = 0;
            DateTime increaseTwoMinute = DateTime.Now.AddMinutes(2);

            while (totalFileCount <= fhirInput.sortedHL7files.Count)
            {
                if (increaseTwoMinute == DateTime.Now)
                {
                    maxDegreeOfParallelism += 10;
                    increaseTwoMinute = DateTime.Now.AddMinutes(2);
                }

                _logger?.LogInformation($"Batch count with Skip: {totalFileCount} and Take: {maxDegreeOfParallelism}");
                var fileList = fhirInput.sortedHL7files.Skip(totalFileCount).Take(maxDegreeOfParallelism).ToList();
                _logger?.LogInformation($"IncreasedTime {increaseTwoMinute}");

                foreach (var item in fileList)
                {
                    var hl7JsonFile = Path.GetFileNameWithoutExtension(item.HL7FileName) + ".json";
                    BlobClient blobClient = blobContainer.GetBlobClient(hl7JsonFile);
                    if (blobClient != null && await blobClient.ExistsAsync())
                    {
                        FhirDetails fhirDetails = new();
                        var blobData = await blobClient.OpenReadAsync();

                        if (fhirReponse.IsFileSkipped)
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
                                var result = await ProcessFhirRequest(fhirDetails, request.ProceedOnError);
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
                totalFileCount += maxDegreeOfParallelism;
                _logger?.LogInformation($"TotalFileCount count : {totalFileCount}");
            }

            return await Task.FromResult(fhirReponse);
        }

        private async Task<FhirResponse> ProcessDataInBatch(FhirInput fhirInput, ProcessRequest request)
        {

            BlobContainerClient blobContainer = new(_blobConfiguration.BlobConnectionString, !request.SkipFhirPostProcess ? _blobConfiguration.HL7FhirPostPorcessJson : _blobConfiguration.FhirJsonContainer);

            FhirResponse fhirReponse = new();
            int maxDegreeOfParallelism = _appConfiguration.UploadFhirJsonMaxParallelism;
            int totalFileCount = 0;
            DateTime increaseTwoMinute = DateTime.Now.AddMinutes(2);

            _logger?.LogInformation($"Total Sortedhl7 Files Count: {fhirInput.sortedHL7files.Count}");
            while (totalFileCount <= fhirInput.sortedHL7files.Count)
            {
                if (increaseTwoMinute.ToString("yyyyMMddhhmmss") == DateTime.Now.ToString("yyyyMMddhhmmss"))
                {
                    maxDegreeOfParallelism += 10;
                    increaseTwoMinute = DateTime.Now.AddMinutes(2);
                }

                _logger?.LogInformation($"Batch count with Skip: {totalFileCount} and Take: {maxDegreeOfParallelism}");
                var fileList = fhirInput.sortedHL7files.Skip(totalFileCount).Take(maxDegreeOfParallelism).ToList();

                if (fhirReponse.IsFileSkipped)
                {
                    UploadSkippedFile(fileList, ref fhirReponse);
                }
                else
                {
                    ParallelOptions parallelOptions = new();
                    parallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;

                    await Parallel.ForEachAsync(fileList, parallelOptions, async (hl7FileName, CancellationToken) =>
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
                                var result = await ProcessFhirRequest(fhirDetails, request.ProceedOnError);
                                if (result != null && result.StatusCode != 200)
                                {
                                    fhirReponse.response.Add(result);
                                }
                                fhirReponse.IsFileSkipped = isFilesSkipped;
                            }
                        }
                    });
                }
                totalFileCount += maxDegreeOfParallelism;
                _logger?.LogInformation($"TotalFileCount count : {totalFileCount}");
            }
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