using HL7Converter.ProcessConverter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

class HL7ConverterFunction
{
    private readonly IConverter _converter;

    public HL7ConverterFunction(IConverter converter)
    {
        _converter = converter;
    }

    [Function(nameof(ConverterOrchestration))]
    public static async Task<string> ConverterOrchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        string result = "";
        string input_context = context.GetInput<string>();
        result += await context.CallActivityAsync<string>(nameof(ConverterActivityTrigger), input_context);
        return result;
    }

    [Function(nameof(ConverterActivityTrigger))]
    public async Task<string> ConverterActivityTrigger([ActivityTrigger] string req, FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(ConverterActivityTrigger));
        logger.LogInformation("Started calling ConverterActivityTrigger");
        return await _converter.Execute(req);
    }

    [Function(nameof(StartConverterHTTP))]
    public static async Task<HttpResponseData> StartConverterHTTP(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger(nameof(StartConverterHTTP));
        string body = new StreamReader(req.Body).ReadToEnd();
        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(ConverterOrchestration), body);
        logger.LogInformation("Created new orchestration with instance ID = {instanceId}", instanceId);
        return client.CreateCheckStatusResponse(req, instanceId);
    }


}