using Azure.Storage.Blobs;
using HL7Validation.Configuration;
using HL7Validation.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NHapi.Base.Parser;
using System.Net;
using System.Text;
using NHapi.Base.Util;


namespace HL7Validation.ValidateMessage
{
    public class ValidateHL7Message : IValidateHL7Message
    {
        public ValidateHL7Message(BlobConfig config, TelemetryClient telemetryClient = null, ILogger<ValidateHL7Message> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _config = config;
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        private readonly string _id;
        private readonly BlobConfig _config;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        public string Id => _id;
        public string Name => "ValidateHL7Message";

        public int MaxParallelism => 230;

        public async Task<HttpResponseData> ValidateMessage(HttpRequestData request)
        {
            DateTime start = DateTime.Now;
            try
            {
                string reqContent = await new StreamReader(request.Body).ReadToEndAsync();
                PostData postData = new();
                if (!string.IsNullOrEmpty(reqContent))
                {
                    postData = JsonConvert.DeserializeObject<PostData>(reqContent);

                    BlobContainerClient blobContainer = new BlobContainerClient(_config.BlobConnectionString, postData.ContainerName);

                    Object listLock = new();
                    List<Hl7Files> validatedHl7Files = new();
                    List<FailHl7Files> failHl7Files = new();
                    List<string> blobList = new();
                    ParallelOptions parallelOptions = new();

                    await foreach (var blob in blobContainer.GetBlobsAsync())
                    {
                        blobList.Add(blob.Name);
                    }


                    if (blobList.Any())
                    {
                        ParallelOptions blobparallelOptions = new();
                        blobparallelOptions.MaxDegreeOfParallelism = MaxParallelism;

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
                                        var parser = new PipeParser { ValidationContext = new Validation.CustomValidation() };
                                        var parsedMessage = parser.Parse(fileData);
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
                                        if (postData.proceedOnError == false)
                                        {
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
                            parallelOptions.MaxDegreeOfParallelism = MaxParallelism;
                            await Parallel.ForEachAsync(validatedHl7Files, parallelOptions, async (hl7File, CancellationToken) =>
                            {
                                BlobContainerClient validatedContainer = new BlobContainerClient(_config.BlobConnectionString, _config.ValidatedBlobContainer);

                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(hl7File.HL7FileData));
                                await validatedContainer.UploadBlobAsync(hl7File.HL7FileName, memoryStream);
                                memoryStream.Close();
                                await blobContainer.DeleteBlobAsync(hl7File.HL7BlobFile);
                            });
                        }

                        if (failHl7Files.Count > 0)
                        {
                            parallelOptions.MaxDegreeOfParallelism = MaxParallelism;
                            await Parallel.ForEachAsync(failHl7Files, parallelOptions, async (failhl7File, CancellationToken) =>
                            {
                                BlobContainerClient hl7validationfaildContainer = new BlobContainerClient(_config.BlobConnectionString, _config.Hl7validationfailBlobContainer);

                                var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(failhl7File.HL7FileData));
                                await hl7validationfaildContainer.UploadBlobAsync(failhl7File.HL7FileName, memoryStream);
                                memoryStream.Close();
                                await blobContainer.DeleteBlobAsync(failhl7File.HL7BlobFile);
                            });
                        }

                        ResponseData responseData = new();
                        responseData.Success = validatedHl7Files.Select(x => new { x.HL7FileName, x.HL7FileType });
                        responseData.Fail = failHl7Files.Select(x => new { x.HL7FileName, x.HL7FileError });
                        var jsonResponseToReturn = JsonConvert.SerializeObject(responseData);
                        var response = request.CreateResponse(HttpStatusCode.OK);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonResponseToReturn));
                        return await Task.FromResult(response);
                    }

                    var jsonEmptyResponseToReturn = JsonConvert.SerializeObject(new List<string>());
                    var emptyBlobresponse = request.CreateResponse(HttpStatusCode.OK);
                    emptyBlobresponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(jsonEmptyResponseToReturn));
                    return await Task.FromResult(emptyBlobresponse);
                }
                var noContentresponse = request.CreateResponse(HttpStatusCode.NoContent);
                return await Task.FromResult(noContentresponse);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id}" + ex.Message, Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(ex.Message));
                return await Task.FromResult(errorResponse);
            }
        }
    }
}
