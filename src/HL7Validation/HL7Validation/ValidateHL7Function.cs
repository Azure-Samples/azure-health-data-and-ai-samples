using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;


namespace Hl7Validation.ValidateMessage
{
    public class ValidateHL7Function
    {
        private readonly IValidateHL7Message _validateHL7;

        public ValidateHL7Function(IValidateHL7Message validateHL7)
        {
            _validateHL7 = validateHL7;
        }

        [Function(nameof(StartHl7Validation))]
        public static async Task<string> StartHl7Validation([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(StartHl7Validation));
            logger.LogInformation($"StartHl7Validation RunOrchestrator call at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
            string input_context = context.GetInput<string>();

            return await context.CallActivityAsync<string>(nameof(ValidateHl7), input_context);
        }

        [Function(nameof(ValidateHl7))]
        public async Task<string> ValidateHl7([ActivityTrigger] string req, FunctionContext executionContext)
        {
            DateTime start = DateTime.Now;
            try
            {
                ILogger logger = executionContext.GetLogger(nameof(ValidateHl7));
                logger.LogInformation($"ValidateHl7 execution start at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                var result = await _validateHL7.ValidateMessage(req);
                logger.LogInformation($"ValidateHl7 execution end at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                return result;
                
            }
            catch (Exception ex)
            {
                throw;
            }

        }


        [Function(nameof(CallHl7Validation))]
        public static async Task<HttpResponseData> CallHl7Validation(
         [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
         [DurableClient] DurableTaskClient client,
         FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CallHl7Validation));
            string body = new StreamReader(req.Body).ReadToEnd();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(StartHl7Validation), body);
            logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
            return client.CreateCheckStatusResponse(req, instanceId);
        }

    }
}
