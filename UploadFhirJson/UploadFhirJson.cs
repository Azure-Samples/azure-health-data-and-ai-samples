using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UploadFhirJson.ProcessFhir;

namespace UploadFhirJson
{
    public class UploadFhirJson
    {
        public UploadFhirJson(IProcessFhirJson processFhirJson, ILoggerFactory loggerFactory)
        {
            _processFhirJson = processFhirJson;
            _logger = loggerFactory.CreateLogger<UploadFhirJson>();
        }
        private readonly IProcessFhirJson _processFhirJson;
        private readonly ILogger _logger;

        [Function("UploadFhirJson")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous,"post", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("UploadFhirJson function processed a request.");
            return await _processFhirJson.Execute(req);
        }
    }
}
