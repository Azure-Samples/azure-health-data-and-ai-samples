using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class SmartConfigurationController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public SmartConfigurationController(IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("/smart/.well-known/smart-configuration")]
        public async Task<IActionResult> GetSmartConfiguration()
        {
            var fhirServerUrl = _config["SmartConfig:FhirServerUrl"];
            var host = $"{Request.Scheme}://{Request.Host}";

            // Fetch native smart-configuration from FHIR service
            var client = _httpClientFactory.CreateClient();
            var nativeUrl = $"{fhirServerUrl}/.well-known/smart-configuration";

            try
            {
                var response = await client.GetAsync(nativeUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);

                    // Override endpoints to point to our server (not Entra directly)
                    doc!["authorization_endpoint"] = JsonSerializer.SerializeToElement($"{host}/auth/authorize");
                    doc!["token_endpoint"] = JsonSerializer.SerializeToElement($"{host}/auth/token");

                    return Ok(doc);
                }
            }
            catch (Exception ex)
            {
                // Log but fall through to fallback
                Console.WriteLine($"Failed to fetch native smart-configuration: {ex.Message}");
            }

            // Fallback: return minimal config if FHIR service is unreachable
            return Ok(new
            {
                authorization_endpoint = $"{host}/auth/authorize",
                token_endpoint = $"{host}/auth/token",
                token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "private_key_jwt" },
                grant_types_supported = new[] { "authorization_code", "client_credentials" },
                scopes_supported = new[] { "openid", "profile", "launch", "launch/patient", "patient/*.rs", "user/*.rs", "offline_access", "fhirUser" },
                response_types_supported = new[] { "code" },
                code_challenge_methods_supported = new[] { "S256" },
                capabilities = new[]
                {
                    "launch-ehr", "launch-standalone", "client-public",
                    "client-confidential-symmetric", "client-confidential-asymmetric",
                    "permission-patient", "permission-user", "permission-offline",
                    "permission-v2", "authorize-post", "sso-openid-connect",
                    "context-ehr-patient", "context-standalone-patient",
                    "context-ehr-encounter", "context-banner", "context-style",
                    "permission-v1"
                }
            });
        }
    }
}
