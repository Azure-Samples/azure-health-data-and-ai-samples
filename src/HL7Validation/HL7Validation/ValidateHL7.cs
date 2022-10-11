using HL7Validation.ValidateMessage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HL7Validation
{
    public class ValidateHL7
    {
        public ValidateHL7(IValidateHL7Message iValidateHL7Message, ILoggerFactory loggerFactory)
        {
            _iValidateHL7Message = iValidateHL7Message;
            _logger = loggerFactory.CreateLogger<ValidateHL7>();

        }
        private readonly IValidateHL7Message _iValidateHL7Message;
        private readonly ILogger _logger;

        [Function("ValidateHL7")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("ValidateHL7 function processed a request.");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (requestBody != null)
            {
                return await _iValidateHL7Message.ValidateMessage(requestBody);
            }
            else
            {
                return await Task.FromResult(new ContentResult() { Content = $"Requested content should not be blank.", StatusCode = 500, ContentType = "text/plain" });
            }
        }
    }
}
