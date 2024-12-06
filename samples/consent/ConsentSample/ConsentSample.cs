using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;

namespace ConsentSample
{
    public class ConsentSample
    {
        private readonly ILogger _logger;
        private readonly IPipeline<HttpRequestData, HttpResponseData> pipeline;

        public ConsentSample(IPipeline<HttpRequestData, HttpResponseData> pipeline, ILoggerFactory loggerFactory)
        {
            this.pipeline = pipeline;
            _logger = loggerFactory.CreateLogger<ConsentSample>();
        }

        [Function("ProcessData")]
        public async Task<HttpResponseData> GetData([HttpTrigger(AuthorizationLevel.Function, "post", "get", "put", "delete", Route ="{*Resource}")] HttpRequestData req)
        {
            // This is what hooks up the Azure Function to the Custom Operation pipeline
            _logger.LogInformation("Patient sample pipeline started...");
            return await pipeline.ExecuteAsync(req);
        }

        //[Function("PostData")]
        //public async Task<HttpResponseData> PostData([HttpTrigger(AuthorizationLevel.Function, "post", Route = "Consent")] HttpRequestData req)
        //{
        //    // This is what hooks up the Azure Function to the Custom Operation pipeline
        //    _logger.LogInformation("Patient sample pipeline started...");
        //    return await pipeline.ExecuteAsync(req);
        //}
    }
}
