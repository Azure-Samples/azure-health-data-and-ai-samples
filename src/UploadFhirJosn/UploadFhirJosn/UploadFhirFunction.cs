using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using UploadFhirJson.ProcessFhir;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace UploadFhirJson
{
    public class UploadFhirFunction
    {
        private readonly IProcessFhirJson _processFhirJson;
        public UploadFhirFunction(IProcessFhirJson processFhirJson)
        {
            _processFhirJson = processFhirJson;
        }

        [Function("RunOrchestrator")]
        public static async Task<string> RunOrchestrator([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            string result = "";
            string input_context = context.GetInput<string>();
            result += await context.CallActivityAsync<string>(nameof(CallActivityTrigger), input_context);
            return result;
        }

        [Function(nameof(CallActivityTrigger))]
        public async Task<string> CallActivityTrigger([ActivityTrigger] string req, FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CallActivityTrigger));
            return await _processFhirJson.Execute(req);
        }

        [Function("UploadFhirJson_HttpStart")]
        public static async Task<HttpResponseData> UploadFhirJson_HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(UploadFhirJson_HttpStart));
            string body = new StreamReader(req.Body).ReadToEnd();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(RunOrchestrator), body);
            logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}