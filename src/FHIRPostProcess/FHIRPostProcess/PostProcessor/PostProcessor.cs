using Azure.Storage.Blobs;
using FHIRPostProcess.Configuration;
using FHIRPostProcess.Model;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace FHIRPostProcess.PostProcessor
{
    public class PostProcess : IPostProcess
    {

        public PostProcess(BlobConfiguration blobConfiguration, FhirJsonParser fhirJsonParser, BlobServiceClient blobServiceClient, TelemetryClient telemetryClient = null, ILogger<PostProcess> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _logger = logger;
            _telemetryClient = telemetryClient;
            _fhirJsonParser = fhirJsonParser;
            _blobConfiguration = blobConfiguration;
            _blobServiceClient = blobServiceClient;
        }

        private readonly string _id;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly FhirJsonParser _fhirJsonParser;
        private readonly BlobConfiguration _blobConfiguration;
        private readonly BlobServiceClient _blobServiceClient;

        public string Id => _id;
        public string Name => "PostProcess";
        private readonly DateTime start = DateTime.Now;



        public async Task<HttpResponseData> PostProcessResources(HttpRequestData httpRequestData)
        {
            FHIRPostProcessInput fHIRPostProcessInput = new();

            try
            {
                _logger?.LogInformation($"Post Process Function start");

                fHIRPostProcessInput = JsonConvert.DeserializeObject<FHIRPostProcessInput>(await new StreamReader(httpRequestData.Body).ReadToEndAsync());
                var hl7FilesArray = System.Text.Encoding.Default.GetString(Convert.FromBase64String(fHIRPostProcessInput.Hl7FilesList));

                if (!string.IsNullOrEmpty(hl7FilesArray))
                {

                    List<Hl7File> hl7FilesList = JsonConvert.DeserializeObject<List<Hl7File>>(hl7FilesArray);
                    if (hl7FilesList != null && hl7FilesList.Count > 0)
                    {

                        _logger?.LogInformation($"Batch count with Skip: {fHIRPostProcessInput.Skip} and Take: {fHIRPostProcessInput.Take}");

                        var hl7FileList = hl7FilesList.Skip(fHIRPostProcessInput.Skip).Take(fHIRPostProcessInput.Take).ToList();

                        ParallelOptions parallelOptions = new();
                        parallelOptions.MaxDegreeOfParallelism = fHIRPostProcessInput.Take;


                        var fhirBundleType = fHIRPostProcessInput.FhirBundleType;
                        fHIRPostProcessInput = null;

                        var blobContainer = _blobServiceClient.GetBlobContainerClient(_blobConfiguration.Hl7ConverterJsonContainer);

                        await Parallel.ForEachAsync(hl7FileList, parallelOptions, async (Hl7File, CancellationToken) =>
                        {
                            if (Hl7File != null && Hl7File.HL7FileName != String.Empty)
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

                                    PostProcessOutput postProcessOutput = new PostProcessOutput
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
                                        await postprocessContainer.UploadBlobAsync(fhirJsonFileName, memoryStream);
                                        memoryStream.Close();
                                        await blobContainer.DeleteBlobAsync(fhirJsonFileName);
                                        _logger?.LogInformation($"Post processing successful and Fhir json uploaded for hl7 file {Hl7File.HL7FileName}.");
                                    }
                                    else
                                    {
                                        _logger?.LogInformation($"Post processing failed for file {Hl7File.HL7FileName}.");
                                        _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));

                                    }
                                }

                            }
                        });

                        var postProcessResponseResult = httpRequestData.CreateResponse(HttpStatusCode.OK);
                        postProcessResponseResult.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Post processing successful"));
                        return await Task.FromResult(postProcessResponseResult);
                    }
                }


                var postProcessResponse = httpRequestData.CreateResponse(HttpStatusCode.BadRequest);
                postProcessResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes($"There are no Hl7 files to perform post processing"));
                return await Task.FromResult(postProcessResponse);

            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"Post processing function failed with exception:{ex.Message}");
                _logger?.LogError(ex, "{Name}-{Id} Error while executing FHIRPostProcess Function App with exception", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var errorResponse = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                errorResponse.Body = new MemoryStream(Encoding.UTF8.GetBytes(ex.Message));
                return await Task.FromResult(errorResponse);
            }
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
    }
}
