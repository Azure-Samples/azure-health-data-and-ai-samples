using Azure.Storage.Blobs;
using FHIRPostProcess.Configuration;
using FHIRPostProcess.Model;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Task = System.Threading.Tasks.Task;

namespace FHIRPostProcess.PostProcessor
{
    public class PostProcess : IPostProcess
    {

        public PostProcess(BlobConfiguration blobConfiguration, AppConfiguration appConfiguration, FhirJsonParser fhirJsonParser, BlobServiceClient blobServiceClient, TelemetryClient telemetryClient = null, ILogger<PostProcess> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _logger = logger;
            _telemetryClient = telemetryClient;
            _fhirJsonParser = fhirJsonParser;
            _blobConfiguration = blobConfiguration;
            _blobServiceClient = blobServiceClient;
            _appConfiguration = appConfiguration;
        }

        private readonly string _id;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly FhirJsonParser _fhirJsonParser;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly AppConfiguration _appConfiguration;

        public string Id => _id;
        public string Name => "PostProcess";
        private readonly DateTime start = DateTime.Now;

        public async Task<string> PostProcessResources(OrchestrationInput orchestrationInput)
        {
            List<Response> responses = new();
            try
            {
                _logger?.LogInformation($"Post Process Function start");
                if (orchestrationInput.Hl7Files != null && orchestrationInput.Hl7Files.Count > 0)
                {
                    var fhirBundleType = orchestrationInput.FhirBundleType;

                    ParallelOptions parallelOptions = new();
                    parallelOptions.MaxDegreeOfParallelism = _appConfiguration.MaxDegreeOfParallelism;

                    var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.Hl7ConverterJsonContainer);
                    _logger?.LogInformation($"Post processing function start at:{DateTime.Now.ToString("yyyyMMdd hh:mm:ss")}");
                    await Parallel.ForEachAsync(orchestrationInput.Hl7Files, parallelOptions, async (Hl7File, CancellationToken) =>
                    {
                        if (Hl7File != null && Hl7File.HL7FileName != string.Empty)
                        {
                            _logger?.LogInformation($"Process start for file {Hl7File.HL7FileName} blob read.");

                            string fhirJsonFileName = string.Empty;
                            string fhirJson = string.Empty;
                            string postProcessBundle = string.Empty;

                            fhirJsonFileName = Path.GetFileNameWithoutExtension(Hl7File.HL7FileName) + ".json";
                            BlobClient blobClient = blobContainer.GetBlobClient(fhirJsonFileName);

                            if (blobClient != null && await blobClient.ExistsAsync())
                            {
                                var blobData = await blobClient.OpenReadAsync();
                                using (var streamReader = new StreamReader(blobData))
                                {
                                    fhirJson = await streamReader.ReadToEndAsync();
                                }

                                _logger?.LogInformation($"Process end for file {Hl7File.HL7FileName} blob read.");

                                if (!string.IsNullOrEmpty(fhirJson))
                                {
                                    Bundle bundleResource = _fhirJsonParser.Parse<Bundle>(fhirJson);

                                    if (fhirBundleType != null && fhirBundleType != string.Empty && fhirBundleType.ToLower() == Bundle.BundleType.Transaction.ToString().ToLower())
                                    {
                                        bundleResource.Type = Bundle.BundleType.Transaction;
                                    }
                                    else
                                    {
                                        bundleResource.Type = Bundle.BundleType.Batch;
                                    }


                                    if (bundleResource != null && bundleResource.Entry.Count > 0)
                                    {
                                        bundleResource.Entry.RemoveAll(e => IsEmptyResource(e.Resource) || IsIdAbsentResource(e.Resource));
                                    }

                                    var postProcessBundlejson = bundleResource.ToJson();

                                    _logger?.LogInformation($"post processing performed for file {Hl7File.HL7FileName}.");
                                    postProcessBundle = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(postProcessBundlejson));

                                }

                                PostProcessOutput postProcessOutput = new()
                                {
                                    HL7FileName = Hl7File.HL7FileName,
                                    HL7Conversion = true,
                                    FhirJson = postProcessBundle,
                                };

                                var postProcessFhirBundle = JsonConvert.SerializeObject(postProcessOutput);

                                if (postProcessFhirBundle != string.Empty)
                                {
                                    var postprocessContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.Hl7PostProcessContainer);
                                    var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(postProcessFhirBundle));
                                    if (!await postprocessContainer.GetBlobClient(fhirJsonFileName).ExistsAsync())
                                    {
                                        await postprocessContainer.UploadBlobAsync(fhirJsonFileName, memoryStream);
                                        memoryStream.Close();
                                    }

                                    await blobContainer.DeleteBlobIfExistsAsync(fhirJsonFileName);

                                    _logger?.LogInformation($"Post processing successful and Fhir json uploaded for hl7 file {Hl7File.HL7FileName}.");
                                }
                                else
                                {
                                    _logger?.LogInformation($"Post processing failed for file {Hl7File.HL7FileName}.");
                                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                                    responses.Add(new Response() { FileName = Hl7File.HL7FileName, Error = "No content found in incoming request." });

                                }
                            }
                        }
                    });
                    _logger?.LogInformation($"Post processing function finish at:{DateTime.Now.ToString("yyyyMMdd hh:mm:ss")}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"Post processing function failed with exception:{ex.Message}");
                _logger?.LogError(ex, "{Name}-{Id} Error while executing FHIRPostProcess Function App with exception", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                responses.Add(new Response() { Error = $"Post processing function failed with exception:{ex.Message}" });
            }
            return await Task.FromResult(responses.Count > 0 ? JsonConvert.SerializeObject(responses) : string.Empty);
        }

        public bool IsEmptyResource(Resource resource)
        {
            try
            {
                var fhirResource = resource.ToJObject();
                var properties = fhirResource.Properties().Select(property => property.Name);
                // an empty resource contains no properties other than "resourceType" and "id"
                return !properties
                    .Where(property => !property.Equals("resourceType"))
                    .Where(property => !property.Equals("id"))
                    .Any();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} IsEmptyResource.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
            }

            return false;
        }

        public bool IsIdAbsentResource(Resource resource)
        {
            return string.IsNullOrWhiteSpace(resource.Id);

        }

        public async Task<string> GetHl7FilesList(string hl7ArrayFileName)
        {
            _logger?.LogInformation($"GetHl7FilesList execution start at:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
            string hl7FilesArray = string.Empty;
            BlobContainerClient blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.ValidatedContainer);
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

            _logger?.LogInformation($"GetHl7FilesList execution end at:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");

            return await Task.FromResult(hl7FilesArray);
        }
    }
}
