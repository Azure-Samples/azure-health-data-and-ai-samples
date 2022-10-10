using FHIRPostProcess.PostProcessor;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FHIRPostProcess
{
    public class FHIRPostProcessFunction
    {
        private readonly ILogger _logger;
        private readonly IPostProcess _postProcess;

        public FHIRPostProcessFunction(IPostProcess postProcess, ILoggerFactory loggerFactory)
        {
            _postProcess = postProcess;
            _logger = loggerFactory.CreateLogger<FHIRPostProcessFunction>();
        }

        [Function("FHIRPostProcessFunction")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "put", Route = null)] HttpRequestData request)
        {
            _logger.LogInformation("FHIRPostProcess function processed a request.");
            return await _postProcess.PostProcessResources(request);
        }
    }
}
