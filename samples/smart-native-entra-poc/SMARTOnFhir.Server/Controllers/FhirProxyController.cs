using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SMARTOnFhir.Server.Configuration;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class FhirProxyController : ControllerBase
    {
        private readonly SmartConfig _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<FhirProxyController> _logger;

        public FhirProxyController(
            IOptions<SmartConfig> config,
            IHttpClientFactory httpClientFactory,
            ILogger<FhirProxyController> logger)
        {
            _config = config.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        [HttpGet("/smart/{**path}")]
        [HttpPost("/smart/{**path}")]
        [HttpPut("/smart/{**path}")]
        [HttpDelete("/smart/{**path}")]
        [HttpPatch("/smart/{**path}")]
        public async Task<IActionResult> ProxyToFhir(string path)
        {
            var client = _httpClientFactory.CreateClient();
            var fhirUrl = $"{_config.FhirServerUrl}/{path}";

            // Append query string if present
            if (Request.QueryString.HasValue)
            {
                fhirUrl += Request.QueryString.Value;
            }

            _logger.LogInformation("Proxying {Method} to {Url}", Request.Method, fhirUrl);

            // Build the outbound request
            var proxyRequest = new HttpRequestMessage(new HttpMethod(Request.Method), fhirUrl);

            // Forward Authorization header (Bearer token from SMART app)
            if (Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
            }

            // Forward Accept header
            if (Request.Headers.TryGetValue("Accept", out var acceptHeader))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Accept", acceptHeader.ToString());
            }

            // Forward Content-Type and body for POST/PUT/PATCH
            if (Request.Method != "GET" && Request.Method != "DELETE" && Request.ContentLength > 0)
            {
                var body = await new StreamReader(Request.Body).ReadToEndAsync();
                proxyRequest.Content = new StringContent(body);
                if (Request.ContentType != null)
                {
                    proxyRequest.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Request.ContentType.Split(';')[0]);
                }
            }

            // Send to FHIR server
            var response = await client.SendAsync(proxyRequest);
            var content = await response.Content.ReadAsStringAsync();
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/fhir+json";

            // Return with same status code
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                Content = content,
                ContentType = contentType
            };
        }
    }
}
