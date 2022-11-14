using HL7Sequencing.Sequencing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace HL7Sequencing
{
    public class HL7SequencingFunction
    {
        private readonly ILogger _logger;
        private readonly ISequence _sequence;


        public HL7SequencingFunction(ISequence sequence, ILoggerFactory loggerFactory)
        {
            _sequence = sequence;
            _logger = loggerFactory.CreateLogger<HL7SequencingFunction>();
        }

        [Function("HL7Sequencing")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function,"post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return await _sequence.GetSequencListAsync(req);
        }
        
    }
}
