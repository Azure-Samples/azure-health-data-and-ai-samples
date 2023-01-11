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

        public PostProcess(FhirJsonParser fhirJsonParser, TelemetryClient telemetryClient = null, ILogger<PostProcess> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _logger = logger;
            _telemetryClient = telemetryClient;
            _fhirJsonParser = fhirJsonParser;          
        }

        private readonly string _id;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly FhirJsonParser _fhirJsonParser;
        public string Id => _id;
        public string Name => "PostProcess";
        private readonly DateTime start = DateTime.Now;



        public async Task<HttpResponseData> PostProcessResources(HttpRequestData request)
        {
            PostProcessInput postProcessInput = new();
            string postProcessBundle = string.Empty;
            try
            {
              
                postProcessInput = JsonConvert.DeserializeObject<PostProcessInput>(await new StreamReader(request.Body).ReadToEndAsync());
                if (postProcessInput != null && postProcessInput.HL7FileName != string.Empty)
                {
                    if(postProcessInput.FhirJson != string.Empty && postProcessInput.HL7Conversion == true)
                    {
                        _logger?.LogInformation($"post process start for file {postProcessInput.HL7FileName}.");
                        string reqBundle = System.Text.Encoding.Default.GetString(Convert.FromBase64String(postProcessInput.FhirJson));
                        
                        Bundle bundleResource = _fhirJsonParser.Parse<Bundle>(reqBundle);

                        if (bundleResource.Type != Bundle.BundleType.Batch)
                        {

                            bundleResource.Type = Bundle.BundleType.Batch;
                        }

                        var resourceList = bundleResource.Entry.ToList();
                        if (resourceList != null && resourceList.Count > 0)
                        {
                            foreach (var e in resourceList)
                            {
                                var resource = e.Resource;
                                var isEmpty = IsEmptyResource(resource);
                                var isAbsent = IsIdAbsentResource(resource);
                                if (isEmpty || isAbsent)
                                {
                                    //remove empty resources from the Fhir Bundle
                                    bundleResource.Entry.Remove(e);
                                }
                            }
                        }
                       var postProcessBundlejson = bundleResource.ToJson();
                       _logger?.LogInformation($"post processing performed for file {postProcessInput.HL7FileName}.");
                       postProcessBundle = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(postProcessBundlejson));
                    }

                    
                    postProcessInput.FhirJson = postProcessBundle;
                    _logger?.LogInformation($"post process FHIR bundle created for file {postProcessInput.HL7FileName}.");

                   

                    var blobFileContent = JsonConvert.SerializeObject(postProcessInput);

                   
                    if (blobFileContent != string.Empty)
                    {
                        
                        var response = request.CreateResponse(HttpStatusCode.OK);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes(blobFileContent));
                         return await Task.FromResult(response);
                    }
                    else
                    {
                        _logger?.LogInformation($"Post processing failed for file {postProcessInput.HL7FileName}.");
                        _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                        _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                        var response = request.CreateResponse(HttpStatusCode.InternalServerError);
                        response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"No content found in incoming request."));
                        return await Task.FromResult(response);
                    }

                }
                else
                {
                    
                    _logger?.LogInformation("{Name}-{Id} No content found in incoming request.", Name, Id);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    var response = request.CreateResponse(HttpStatusCode.InternalServerError);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"No content found in incoming request."));
                    return await Task.FromResult(response);
                }

            }
            catch (Exception ex)
            {
                _logger?.LogInformation($"Post processing execption for file {postProcessInput.HL7FileName}  with exception:{ex.Message}");
                _logger?.LogError(ex, "{Name}-{Id} Post processing message.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
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
