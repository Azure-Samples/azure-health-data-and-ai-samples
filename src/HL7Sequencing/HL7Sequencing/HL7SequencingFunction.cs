using HL7Sequencing.Sequencing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace HL7Sequencing
{
    public class HL7SequencingFunction
    {
        private readonly ISequence _sequence;
        public HL7SequencingFunction(ISequence HL7Sequencing)
        {
            _sequence = HL7Sequencing;
        }

        [Function(nameof(StartHL7Sequencing))]
        public static async Task<string> StartHL7Sequencing([OrchestrationTrigger] TaskOrchestrationContext context)
        {
            ILogger logger = context.CreateReplaySafeLogger(nameof(StartHL7Sequencing));
            logger.LogInformation($"StartHL7Sequencing RunOrchestrator call at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
            string input_context = context.GetInput<string>();

            return await context.CallActivityAsync<string>(nameof(HL7Sequencing), input_context); ;
        }

        [Function(nameof(HL7Sequencing))]
        public async Task<string> HL7Sequencing([ActivityTrigger] string req, FunctionContext executionContext)
        {
            try
            {
                ILogger logger = executionContext.GetLogger(nameof(HL7Sequencing));
                logger.LogInformation($"HL7Sequencing execution start at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                var result = await _sequence.GetSequencListAsync(req);
                logger.LogInformation($"HL7Sequencing execution end at time:{DateTime.Now.ToString("yyyyMMdd HH:mm:ss")}");
                return result;

            }
            catch (Exception ex)
            {
                throw;
            }

        }

        [Function(nameof(CallHL7Sequencing))]
        public static async Task<HttpResponseData> CallHL7Sequencing(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger(nameof(CallHL7Sequencing));

            string body = new StreamReader(req.Body).ReadToEnd();
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(StartHL7Sequencing), body);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return client.CreateCheckStatusResponse(req, instanceId);
        }
    }
}
