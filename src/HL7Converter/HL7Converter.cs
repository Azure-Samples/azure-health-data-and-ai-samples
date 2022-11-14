using HL7Converter.ProcessConverter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HL7Converter
{
    public class HL7Converter
    {
        public HL7Converter(IConverter converter, ILoggerFactory loggerFactory)
        {
            _converter = converter;
            _logger = loggerFactory.CreateLogger<HL7Converter>();
        }

        private readonly ILogger _logger;
        private readonly IConverter _converter;

        [Function("HL7Converter")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestData req)
        {
            _logger.LogInformation("HL7Converter function processed a request.");
            return await _converter.Execute(req);
        }
    }
}
