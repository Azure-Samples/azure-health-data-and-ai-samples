using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace FHIRPostProcess.PostProcessor
{
    public class PostProcess : IPostProcess
    {

        public PostProcess(TelemetryClient telemetryClient = null, ILogger<PostProcess> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        private readonly string _id;
        private readonly ILogger _logger;
        private readonly TelemetryClient _telemetryClient;
        public string Id => _id;
        public string Name => "PostProcess";
        private readonly DateTime start = DateTime.Now;



        public async Task<HttpResponseData> PostProcessResources(HttpRequestData request)
        {

            try
            {
                FhirJsonParser _parser = new();
                string reqBundle = await new StreamReader(request.Body).ReadToEndAsync();
                Bundle bundleResource = _parser.Parse<Bundle>(reqBundle);

                if (bundleResource.Type != Bundle.BundleType.Transaction)
                {

                    bundleResource.Type = Bundle.BundleType.Transaction;
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

                var postProcessBundle = bundleResource.ToJson();

                var response = request.CreateResponse(HttpStatusCode.OK);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes(postProcessBundle));
                return response;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} validation message.", Name, Id);
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
