using Azure.Storage.Blobs;
using HL7Converter.Configuration;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace HL7Converter.ProcessConverter
{
    public class Converter : IConverter
    {
        public Converter(BlobConfiguration blobConfiguration, IFhirClient fhirClient, TelemetryClient telemetryClient = null, ILogger<Converter> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _blobConfiguration = blobConfiguration;
            _fhirClient = fhirClient;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly ILogger _logger;
        private readonly IFhirClient _fhirClient;
        public string Id => _id;
        public string Name => "HL7Converter";


        public async Task<HttpResponseData> Execute(HttpRequestData httpRequestData)
        {
            DateTime start = DateTime.Now;
            HttpResponseMessage httpResponseMessage = new();
            try
            {
                HL7Input hl7Input = JsonConvert.DeserializeObject<HL7Input>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                if (hl7Input != null && !string.IsNullOrEmpty(hl7Input.ConversionBody))
                {
                    _logger?.LogInformation($"Processing {hl7Input.HL7FileName} file to fhir server.");
                    string requestBody = System.Text.Encoding.Default.GetString(Convert.FromBase64String(hl7Input.ConversionBody));
                    httpResponseMessage = await _fhirClient.Send(requestBody);
                    var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        _logger?.LogInformation($"Fhir request completed successfully.");
                        await UploadToBlob(hl7Input.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConvertedContainer);
                        var response = httpRequestData.CreateResponse(httpResponseMessage.StatusCode);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                        return await Task.FromResult(response);
                    }
                    else
                    {
                        _logger?.LogInformation("{Name}-{Id} Error from Fhir server.", Name, Id);
                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                        await UploadToBlob(hl7Input.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConversionfailContainer);
                        var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                        return await Task.FromResult(response);
                    }
                }
                else
                {
                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    await UploadToBlob(hl7Input.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConversionfailContainer);
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
            BlobContainerClient sourceClient = new(_blobConfiguration.BlobConnectionString, soruceBlobName);
            BlobContainerClient targetClient = new(_blobConfiguration.BlobConnectionString, targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            await sourceBlobClient.DeleteAsync();
        }
    }
}
