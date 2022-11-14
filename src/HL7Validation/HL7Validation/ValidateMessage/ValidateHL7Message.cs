using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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

                    List<Hl7Files> validatedHl7Files = new();
                    List<FailHl7Files> failHl7Files = new();

                    await foreach (BlobItem blob in blobContainer.GetBlobsAsync())
                    {

                        BlobClient blobClient = blobContainer.GetBlobClient(blob.Name);
                        if (blobClient != null && await blobClient.ExistsAsync())
                        {
                            string fileData = string.Empty;
                            Hl7Files hl7Files = new();
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
                                    hl7Files.HL7FileData = fileData;
                                    hl7Files.HL7FileName = Path.GetFileNameWithoutExtension(blob.Name) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(blob.Name);
                                    hl7Files.HL7BlobFile = blob.Name;
                                    var parser = new PipeParser { ValidationContext = new Validation.CustomValidation() };


                                    var parsedMessage = parser.Parse(fileData);
                                    if (parsedMessage != null)
                                    {
                                        hl7Files.HL7FileType = parsedMessage?.GetType().Name;
                                        validatedHl7Files.Add(hl7Files);

                                    }
                                }
                                catch (Exception ex)
                                {
                                    var exMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                                    _logger?.LogError(ex, "{Name}-{Id} " + exMessage, Name, Id);
                                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                                    if (postData.proceedOnError == false)
                                    {
                                        var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                                        errorResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(blob.Name + " "+exMessage));
                                        return await Task.FromResult(errorResponse);
                                    }
                                    FailHl7Files failHl7File = new();
                                    failHl7File.HL7FileName = Path.GetFileNameWithoutExtension(blob.Name) + "_" + DateTime.Now.ToString("MMddyyyyhhmmss") + Path.GetExtension(blob.Name);
                                    failHl7File.HL7BlobFile = blob.Name;
                                    failHl7File.HL7FileData = fileData;
                                    failHl7File.HL7FileError = exMessage;
                                    failHl7Files.Add(failHl7File);

                                }
                            }

                        }

                    }

                    if (validatedHl7Files.Any() || failHl7Files.Any())
                    {

                        foreach (Hl7Files hl7File in validatedHl7Files)
                        {
                            BlobContainerClient validatedContainer = new BlobContainerClient(_config.BlobConnectionString, _config.ValidatedBlobContainer);

                            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(hl7File.HL7FileData));
                            await validatedContainer.UploadBlobAsync(hl7File.HL7FileName, memoryStream);
                            memoryStream.Close();
                            await blobContainer.DeleteBlobAsync(hl7File.HL7BlobFile);
                        }


                        foreach (FailHl7Files failhl7File in failHl7Files)
                        {
                            BlobContainerClient hl7validationfaildContainer = new BlobContainerClient(_config.BlobConnectionString, _config.Hl7validationfailBlobContainer);

                            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(failhl7File.HL7FileData));
                            await hl7validationfaildContainer.UploadBlobAsync(failhl7File.HL7FileName, memoryStream);
                            memoryStream.Close();
                            await blobContainer.DeleteBlobAsync(failhl7File.HL7BlobFile);
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
