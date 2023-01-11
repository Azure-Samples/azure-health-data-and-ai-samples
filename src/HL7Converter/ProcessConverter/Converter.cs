using Azure.Storage.Blobs;
using HL7Converter.Configuration;
using HL7Converter.FhirClient;
using HL7Converter.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Text;

namespace HL7Converter.ProcessConverter
{
    public class Converter : IConverter
    {
        public Converter(BlobConfiguration blobConfiguration, AppConfiguration appConfiguration, IFhirClient fhirClient, BlobServiceClient blobServiceClient, TelemetryClient telemetryClient = null, ILogger<Converter> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _blobConfiguration = blobConfiguration;
            _fhirClient = fhirClient;
            _blobServiceClient = blobServiceClient;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly AppConfiguration _appConfiguration;
        private readonly ILogger _logger;
        private readonly IFhirClient _fhirClient;
        private readonly BlobServiceClient _blobServiceClient;
        public string Id => _id;
        public string Name => "HL7Converter";

      
        public async Task<HttpResponseData> Execute(HttpRequestData httpRequestData)
        {
            DateTime start = DateTime.Now;
            HttpResponseMessage httpResponseMessage = new();
            try
            {
                HL7Input hl7Input = JsonConvert.DeserializeObject<HL7Input>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                if (hl7Input != null && !string.IsNullOrEmpty(hl7Input.HL7FileName) && !string.IsNullOrEmpty(hl7Input.ConversionBody))
                {
                    
                    _logger?.LogInformation($"Process start for file {hl7Input.HL7FileName} blob read.");
                    string hL7fileContent = string.Empty;
                    var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.ValidatedContainer);
                    BlobClient blobClient = blobContainer.GetBlobClient(hl7Input.HL7FileName);

                    if (blobClient != null && await blobClient.ExistsAsync())
                    {
                        var blobData = await blobClient.OpenReadAsync();
                        using (var streamReader = new StreamReader(blobData))
                        {
                            hL7fileContent = await streamReader.ReadToEndAsync();
                        }


                        _logger?.LogInformation($"Process end for file {hl7Input.HL7FileName} blob read.");

                        if (!string.IsNullOrEmpty(hL7fileContent))
                        {

                            string reqJson = System.Text.Encoding.Default.GetString(Convert.FromBase64String(hl7Input.ConversionBody));
                            JObject jObject = Newtonsoft.Json.JsonConvert.DeserializeObject(reqJson) as JObject;

                            jObject["parameter"][0]["valueString"] = hL7fileContent;

                            string requestBody = Newtonsoft.Json.JsonConvert.SerializeObject(jObject, Newtonsoft.Json.Formatting.Indented);

                            _logger?.LogInformation($"Processing {hl7Input.HL7FileName} file to fhir server.");
                            httpResponseMessage = await _fhirClient.Send(requestBody);
                            var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                            if (httpResponseMessage.IsSuccessStatusCode)
                            {
                                _logger?.LogInformation($"Fhir request completed successfully with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {hl7Input.HL7FileName}.");
                                await UploadToBlob(hl7Input.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConvertedContainer);
                                var response = httpRequestData.CreateResponse(httpResponseMessage.StatusCode);
                                response.Body = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                                return await Task.FromResult(response);
                            }
                            else
                            {
                                _logger?.LogInformation($"Error from Fhir server with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {hl7Input.HL7FileName}");
                                _logger?.LogInformation("{Name}-{Id} Error from Fhir server.", Name, Id);
                                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
  
                                var isHttpRetryStatusCode = _appConfiguration.HttpFailStatusCodes.Split(',').Any(x => x.Trim() == ((int)httpResponseMessage.StatusCode).ToString());

                                _logger?.LogInformation($"failureCode check result  = {isHttpRetryStatusCode} for file {hl7Input.HL7FileName}");

                                if (!isHttpRetryStatusCode)
                                {
                                    if (hl7Input.ProceedOnError == true)
                                    {
                                        await UploadToBlob(hl7Input.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConversionfailContainer);
                                    }
                                }

                                var response = httpRequestData.CreateResponse(httpResponseMessage.StatusCode);
                                response.Body = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                                return await Task.FromResult(response);
                            }
                        }
                        else
                        {
                            _logger?.LogInformation("{Name}-{Id} No content found in HL7 File.", Name, Id);
                            _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                            var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                            response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"No content found in HL7 File."));
                            return await Task.FromResult(response);
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("{Name}-{Id} HL7 File Not found in a Container or alreday Processed.", Name, Id);
                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                        var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"HL7 File Not found in a Container or already Processed."));
                        return await Task.FromResult(response);
                    }
                }
                else
                {
                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
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

        public async Task UploadToBlob(string fileName, string soruceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            await sourceBlobClient.DeleteAsync();
        }
    }
}
