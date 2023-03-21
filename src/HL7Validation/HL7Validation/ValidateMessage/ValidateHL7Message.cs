using Azure.Storage.Blobs;
using Hl7Validation.Configuration;
using Hl7Validation.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NHapi.Base.Parser;
using System.Text;
using NHapi.Base.Util;
using Azure.Storage.Blobs.Models;


namespace Hl7Validation.ValidateMessage
{
    public class ValidateHL7Message : IValidateHL7Message
    {
        public ValidateHL7Message(BlobConfig config, AppConfiguration appConfiguration, PipeParser pipeParser = null, BlobServiceClient blobServiceClient = null, TelemetryClient telemetryClient = null, ILogger<ValidateHL7Message> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _config = config;
            _logger = logger;
            _telemetryClient = telemetryClient;
            _pipeParser = pipeParser;
            _blobServiceClient = blobServiceClient;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly BlobConfig _config;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly PipeParser _pipeParser;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly AppConfiguration _appConfiguration;
        public string Id => _id;
        public string Name => "ValidateHL7Message";

       
        public async Task<string> ValidateMessage(string request)
        {
            DateTime start = DateTime.Now;
            
            bool proceedOnError = true;
            List<string> blobList = new();
            string hl7FileContainer = string.Empty;
            int maxDegreeOfParallelism;
            try
            {
                //string reqContent = await new StreamReader(request).ReadToEndAsync();
                PostData postData = new();
                if (!string.IsNullOrEmpty(request))
                {
                    _logger?.LogInformation($"HL7Validation start at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                    postData = JsonConvert.DeserializeObject<PostData>(request);

                    var blobContainer = _blobServiceClient.GetBlobContainerClient(postData.ContainerName);
                    maxDegreeOfParallelism = _appConfiguration.Hl7validationMaxParallelism;
                    hl7FileContainer = postData.ContainerName;
                    Object listLock = new();
                    List<Hl7Files> validatedHl7Files = new();
                    List<FailHl7Files> failHl7Files = new();
                    ParallelOptions parallelOptions = new();

                    await foreach (var blob in blobContainer.GetBlobsAsync())
                    {
                        blobList.Add(blob.Name);
                    }

                    if (blobList.Any())
                    {
                        
                        ParallelOptions blobparallelOptions = new();
                        blobparallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;

                        await Parallel.ForEachAsync(blobList, blobparallelOptions, async (blob, CancellationToken) =>
                        {
                            BlobClient blobClient = blobContainer.GetBlobClient(blob);
                            if (blobClient != null && await blobClient.ExistsAsync())
                            {
                                string fileData = string.Empty;

                                var blobData = await blobClient.OpenReadAsync();
                                using (var streamReader = new StreamReader(blobData))
                                {
                                    fileData = await streamReader.ReadToEndAsync();
                                }

                                //Use NHapi to read HL7 messages                            
                                if (fileData != string.Empty && fileData != null)
                                {
                                    try
                                    {
                                        var parsedMessage = _pipeParser.Parse(fileData);
                                        if (parsedMessage != null)
                                        {
                                            var terser = new Terser(parsedMessage);
                                            Hl7Files hl7Files = new();
                                            hl7Files.HL7FileType = terser.Get("/MSH-9-1") + "_" + terser.Get("/MSH-9-2");
                                            hl7Files.HL7FileData = fileData;
                                            hl7Files.HL7FileName = Path.GetFileNameWithoutExtension(blob) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(blob);
                                            hl7Files.HL7BlobFile = blob;

                                            lock (listLock)
                                            {
                                                validatedHl7Files.Add(hl7Files);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        var exMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                                        _logger?.LogError(ex, "{Name}-{Id} " + exMessage, Name, Id);
                                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                                        if (postData.ProceedOnError == false)
                                        {
                                            proceedOnError = false;
                                            throw new Exception("FileName: " + blob + ",Error Message: " + exMessage);
                                        }
                                        FailHl7Files failHl7File = new();
                                        failHl7File.HL7FileName = Path.GetFileNameWithoutExtension(blob) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(blob);
                                        failHl7File.HL7BlobFile = blob;
                                        failHl7File.HL7FileData = fileData;
                                        failHl7File.HL7FileError = exMessage;

                                        lock (listLock)
                                        {
                                            failHl7Files.Add(failHl7File);
                                        }
                                    }
                                }
                            }
                        });
                    }

                    if (validatedHl7Files.Any() || failHl7Files.Any())
                    {
                        if (validatedHl7Files.Count > 0)
                        {
                            _logger?.LogInformation($"Valid Hl7File count: " + validatedHl7Files.Count);
                            parallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
                            await Parallel.ForEachAsync(validatedHl7Files, parallelOptions, async (hl7File, CancellationToken) =>
                            {
                                var validatedContainer = _blobServiceClient.GetBlobContainerClient(_config.ValidatedBlobContainer);
                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(hl7File.HL7FileData));
                                await validatedContainer.UploadBlobAsync(hl7File.HL7FileName, memoryStream);
                                memoryStream.Close();
                                await blobContainer.DeleteBlobAsync(hl7File.HL7BlobFile);
                                _logger?.LogInformation($"Valid Hl7File uploaded with name: " + hl7File.HL7FileName);
                            });
                        }

                        if (failHl7Files.Count > 0)
                        {
                            parallelOptions.MaxDegreeOfParallelism = maxDegreeOfParallelism;
                            await Parallel.ForEachAsync(failHl7Files, parallelOptions, async (failhl7File, CancellationToken) =>
                            {
                                var hl7validationfaildContainer = _blobServiceClient.GetBlobContainerClient(_config.Hl7validationfailBlobContainer);
                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(failhl7File.HL7FileData));
                                await hl7validationfaildContainer.UploadBlobAsync(failhl7File.HL7FileName, memoryStream);
                                memoryStream.Close();
                                await blobContainer.DeleteBlobAsync(failhl7File.HL7BlobFile);
                            });
                        }

                        ResponseData responseData = new();
                        var SuccessHl7Files = validatedHl7Files.Select(x => new { x.HL7FileName, x.HL7FileType });
                        
                        string SuccessHl7FilesJson = JsonConvert.SerializeObject(SuccessHl7Files);
                        string Hl7FilesArrayFileName = "Hl7FilesArray" + "_" + DateTime.Now.ToString("MMddyyyyhhmmss")+".json";

                        var validatedContainer = _blobServiceClient.GetBlobContainerClient(_config.ValidatedBlobContainer);

                        if (!await validatedContainer.GetBlobClient(Hl7FilesArrayFileName).ExistsAsync())
                        {
                            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(SuccessHl7FilesJson));
                            await validatedContainer.UploadBlobAsync(Hl7FilesArrayFileName, memoryStream);
                            memoryStream.Close();
                        }

                        responseData.Success = Hl7FilesArrayFileName;
                        responseData.Fail = failHl7Files.Select(x => new { x.HL7FileName, x.HL7FileError });
                        var jsonResponseToReturn = JsonConvert.SerializeObject(responseData);
                        _logger?.LogInformation($"HL7Validation completed at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                        return await Task.FromResult(jsonResponseToReturn);
                    }

                    ResponseData emptyResponseData = new();
                    emptyResponseData.Success = string.Empty;
                    emptyResponseData.Fail = Enumerable.Empty<string>();
                    var jsonEmptyResponseToReturn = JsonConvert.SerializeObject(emptyResponseData);
                    return await Task.FromResult(jsonEmptyResponseToReturn);
                }

                ResponseData reqemptyResponseData = new();
                reqemptyResponseData.Success = string.Empty;
                reqemptyResponseData.Fail = Enumerable.Empty<string>();
                var jsonReqEmptyResponseToReturn = JsonConvert.SerializeObject(reqemptyResponseData);
                return await Task.FromResult(jsonReqEmptyResponseToReturn);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id}" + ex.Message, Name, Id);
                if (!proceedOnError && blobList.Any() && !string.IsNullOrEmpty(hl7FileContainer))
                {
                    foreach (var blob in blobList)
                    {
                        await UploadToBlob(blob, hl7FileContainer, _config.Hl7skippedContainer);
                    }

                    //move all hl7 files from Validated container to hl7skipped container
                    await MoveFilesFromSourceToTarget(_config.ValidatedBlobContainer, _config.Hl7skippedContainer);
                }

                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                throw;
            }
        }

        public async Task UploadToBlob(string fileName, string sourceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(sourceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            if (await sourceBlobClient.ExistsAsync() && !await targetBlobClient.ExistsAsync())
            {
                await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                await sourceBlobClient.DeleteAsync();
            }

        }

        public async Task MoveFilesFromSourceToTarget(string sourceContainerName, string targetContainerName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(sourceContainerName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetContainerName);

            await foreach (var blobfile in sourceClient.GetBlobsAsync())
            {
                BlobClient sourceBlobClient = sourceClient.GetBlobClient(blobfile.Name);
                BlobClient targetBlobClient = targetClient.GetBlobClient(blobfile.Name);
                if (await sourceBlobClient.ExistsAsync() && !await targetBlobClient.ExistsAsync())
                {
                    await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                    await sourceBlobClient.DeleteAsync();
                }
            }
        }
    }
}
