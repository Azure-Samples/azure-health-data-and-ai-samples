using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NHapi.Base.Parser;

namespace HL7Validation.ValidateMessage
{
    public class ValidateHL7Message : IValidateHL7Message
    {
        public ValidateHL7Message(TelemetryClient telemetryClient = null, ILogger<ValidateHL7Message> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
        }

        private readonly string _id;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger _logger;
        public string Id => _id;
        public string Name => "ValidateHL7Message";

        public async Task<ContentResult> ValidateMessage(string hl7Message)
        {
            DateTime start = DateTime.Now;

            try
            {
                //make the parser use 'StrictValidation'
                var parser = new PipeParser { ValidationContext =new Validation.CustomValidation() };
                var parsedMessage = parser.Parse(hl7Message);                
                return await Task.FromResult(new ContentResult() { Content = parsedMessage?.GetType().Name, StatusCode = 200, ContentType = "text/plain" });

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} validation message.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                return await Task.FromResult(new ContentResult() { Content = $"Error while validating message:{(ex.InnerException!=null?ex.InnerException:ex.Message)}", StatusCode = 500, ContentType = "text/plain" });
            }
        }
    }
}
