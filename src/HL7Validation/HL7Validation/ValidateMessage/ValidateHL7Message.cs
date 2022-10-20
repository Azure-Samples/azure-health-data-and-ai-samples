using Azure.Core;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using NHapi.Base.Parser;
using System.Net;
using System.Text;

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

        public async Task<HttpResponseData> ValidateMessage(HttpRequestData httpRequestData)
        {
            DateTime start = DateTime.Now;

            try
            {
                string hl7Message = await new StreamReader(httpRequestData.Body).ReadToEndAsync();
                if (httpRequestData != null)
                {
                    //make the parser use 'StrictValidation'
                    var parser = new PipeParser { ValidationContext = new Validation.CustomValidation() };
                    var parsedMessage = parser.Parse(hl7Message);
                    var response = httpRequestData.CreateResponse(HttpStatusCode.OK);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes(parsedMessage?.GetType().Name));
                    return await Task.FromResult(response);
                }
                else
                {
                    var response = httpRequestData.CreateResponse(HttpStatusCode.BadRequest);
                    response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Requested content should not be blank."));
                    return await Task.FromResult(response);
                }

            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "{Name}-{Id} validation message.", Name, Id);
                _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                var response = httpRequestData.CreateResponse(HttpStatusCode.InternalServerError);
                response.Body = new MemoryStream(Encoding.UTF8.GetBytes($"Error while validating message:{(ex.InnerException != null ? ex.InnerException : ex.Message)}"));
                return await Task.FromResult(response);
            }
        }
    }
}
