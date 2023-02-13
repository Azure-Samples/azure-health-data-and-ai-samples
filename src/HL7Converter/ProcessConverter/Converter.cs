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
            string hl7ConverterRequestTemplate = "{\"parameter\":[{\"name\":\"inputData\",\"valueString\":\"\"},{\"name\":\"inputDataType\",\"valueString\":\"Hl7v2\"},{\"name\":\"templateCollectionReference\",\"valueString\":\"microsofthealth/fhirconverter:default\"},{\"name\":\"rootTemplate\",\"valueString\":\"\"}],\"resourceType\":\"Parameters\"}";
            HttpResponseMessage httpResponseMessage = new();
            try
            {
                _logger?.LogInformation($"hl7Converter Function start"); 

                HL7ConverterInput hL7ConverterInput = JsonConvert.DeserializeObject<HL7ConverterInput>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                var hl7FilesArray = System.Text.Encoding.Default.GetString(Convert.FromBase64String(hL7ConverterInput.Hl7FileList));
                if(!string.IsNullOrEmpty(hl7FilesArray))
                {
                    List<Hl7File> hl7FilesList = JsonConvert.DeserializeObject<List<Hl7File>>(hl7FilesArray);

                    if (hl7FilesList != null && hl7FilesList.Count > 0)
                    {

                        _logger?.LogInformation($"Batch count with Skip: {hL7ConverterInput.Skip} and Take: {hL7ConverterInput.Take}");

                        var hl7FileList = hl7FilesList.Skip(hL7ConverterInput.Skip).Take(hL7ConverterInput.Take).ToList();

                        ParallelOptions parallelOptions = new();
                        parallelOptions.MaxDegreeOfParallelism = hL7ConverterInput.Take;

                        await Parallel.ForEachAsync(hl7FileList, parallelOptions, async (Hl7File, CancellationToken) =>
                        {
                            if (Hl7File != null && Hl7File.HL7FileName != String.Empty && Hl7File.HL7FileType != String.Empty)
                            {

                                string hL7fileContent = string.Empty;
                                var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.ValidatedContainer);
                                BlobClient blobClient = blobContainer.GetBlobClient(Hl7File.HL7FileName);

                                if (blobClient != null && await blobClient.ExistsAsync())
                                {
                                    var blobData = await blobClient.OpenReadAsync();
                                    using (var streamReader = new StreamReader(blobData))
                                    {
                                        hL7fileContent = await streamReader.ReadToEndAsync();
                                    }

                                    if (!string.IsNullOrEmpty(hL7fileContent))
                                    {

                                        JObject jObject = Newtonsoft.Json.JsonConvert.DeserializeObject(hl7ConverterRequestTemplate) as JObject;

                                        jObject["parameter"][0]["valueString"] = hL7fileContent;
                                        jObject["parameter"][3]["valueString"] = Hl7File.HL7FileType;

                                        string requestBody = Newtonsoft.Json.JsonConvert.SerializeObject(jObject, Newtonsoft.Json.Formatting.Indented);

                                        try
                                        {

                                            _logger?.LogInformation($"Processing {Hl7File.HL7FileName} file to fhir server.");
                                            httpResponseMessage = await _fhirClient.Send(requestBody, Hl7File.HL7FileName);
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogInformation($"Error while sending Fhir request to Converter with exception:{ex.Message}");
                                            _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

                                        }


                                        var responseString = await httpResponseMessage.Content.ReadAsStringAsync() ?? "{}";
                                        if (httpResponseMessage.IsSuccessStatusCode)
                                        {
                                            _logger?.LogInformation($"Fhir request completed successfully with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {Hl7File.HL7FileName}.");
                                            await UploadToBlob(Hl7File.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConvertedContainer);

                                            var validatedContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.Hl7ConverterJsonContainer);
                                            var fhirBundleJson = new MemoryStream(Encoding.UTF8.GetBytes(responseString));
                                            string fhirJsonFileName = Path.GetFileNameWithoutExtension(Hl7File.HL7FileName) + ".json";

                                            if (!await validatedContainer.GetBlobClient(fhirJsonFileName).ExistsAsync())
                                            {
                                                await validatedContainer.UploadBlobAsync(fhirJsonFileName, fhirBundleJson);
                                                fhirBundleJson.Close();
                                            }
                                        }
                                        else
                                        {
                                            _logger?.LogInformation($"Error from Fhir server with status code {(int)httpResponseMessage.StatusCode} and {httpResponseMessage.ReasonPhrase} for file {Hl7File.HL7FileName}");
                                            _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

                                            var isHttpRetryStatusCode = _appConfiguration.HttpFailStatusCodes.Split(',').Any(x => x.Trim() == ((int)httpResponseMessage.StatusCode).ToString());

                                            _logger?.LogInformation($"failureCode check result  = {isHttpRetryStatusCode} for file {Hl7File.HL7FileName}");

                                            await UploadToBlob(Hl7File.HL7FileName, _blobConfiguration.ValidatedContainer, _blobConfiguration.ConversionfailContainer);

                                        }
                                    }
                                    else
                                    {
                                        _logger?.LogInformation($"content not found for file {Hl7File.HL7FileName}.");

                                    }
                                }
                            }
                        });


                        var hl7ConverterResponse = httpRequestData.CreateResponse(HttpStatusCode.OK);
                        hl7ConverterResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Hl7 Conversion successful"));
                        return await Task.FromResult(hl7ConverterResponse);
                    }
                }

                _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"No content found in incoming request."));
                return await Task.FromResult(response);

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} Error while executing HL7Convereter Function App with exception", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Error while executing HL7Convereter Function app with exception :{(ex.InnerException != null ? ex.InnerException : ex.Message)}"));
                return await Task.FromResult(response);
            }
        }

        public async Task UploadToBlob(string fileName, string soruceBlobName, string targetBloblName)
        {
            var sourceClient = _blobServiceClient.GetBlobContainerClient(soruceBlobName);
            var targetClient = _blobServiceClient.GetBlobContainerClient(targetBloblName);
            BlobClient sourceBlobClient = sourceClient.GetBlobClient(fileName);
            BlobClient targetBlobClient = targetClient.GetBlobClient(fileName);
            if (await sourceBlobClient.ExistsAsync() && !await targetBlobClient.ExistsAsync())
            {
                await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                await sourceBlobClient.DeleteAsync();
            }

        }
    }
}
