using HL7Validation.ValidateMessage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HL7Validation
{
    public class ValidateHL7Function
    {
        private readonly ILogger _logger;
        private readonly IValidateHL7Message _validateHL7;

        public ValidateHL7Function(IValidateHL7Message validateHL7, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ValidateHL7Function>();
            _validateHL7 = validateHL7;
        }

        [Function("ValidateHL7")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return await _validateHL7.ValidateMessage(req);
        }
    }
}
