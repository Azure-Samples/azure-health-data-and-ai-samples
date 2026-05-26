using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SMARTOnFhir.Server.Configuration;
using SMARTOnFhir.Server.Models;
using SMARTOnFhir.Server.Services;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly SmartConfig _config;
        private readonly ILogger<TokenController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;

        public TokenController(
            IOptions<SmartConfig> config,
            ILogger<TokenController> logger,
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _config = config.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _cache = cache;
        }

        [HttpPost("/auth/token")]
        public async Task<IActionResult> Token()
        {
            // Validate content type
            if (!Request.ContentType?.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) ?? true)
            {
                return BadRequest("Content-Type must be application/x-www-form-urlencoded");
            }

            var form = await Request.ReadFormAsync();
            var grantType = form["grant_type"].ToString();

            _logger.LogInformation("Token request received. grant_type={GrantType}", grantType);

            return grantType switch
            {
                "authorization_code" => await HandleAuthorizationCode(form),
                "refresh_token" => await HandleRefreshToken(form),
                "client_credentials" => await HandleClientCredentials(form),
                _ => BadRequest($"Unsupported grant_type: {grantType}")
            };
        }

        /// <summary>
        /// Handles authorization_code grant — public (PKCE) and confidential clients.
        /// The "code" is forwarded directly to Entra. After Entra issues the token,
        /// EnrichWithLaunchContext reads any per-user launch context cached under
        /// "launch:{oid}" (populated by the launcher via /auth/context-cache) and
        /// injects patient/encounter/fhirUser/etc. into the token response.
        /// </summary>
        private async Task<IActionResult> HandleAuthorizationCode(IFormCollection form)
        {
            var code = form["code"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var clientId = form["client_id"].ToString();
            var codeVerifier = form["code_verifier"].ToString();
            var clientSecret = form["client_secret"].ToString();
            var scope = form["scope"].ToString();

            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(clientId))
            {
                return BadRequest("Missing required parameters: code, client_id");
            }

            // Build form data for Entra token endpoint
            var entraParams = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri
            };

            // Forward scope if provided
            if (!string.IsNullOrEmpty(scope))
            {
                entraParams["scope"] = SmartScopeMapper.ToEntraFormat(scope, _config.FhirAudience);
            }

            // Public client (PKCE) — has code_verifier, no client_secret
            if (!string.IsNullOrEmpty(codeVerifier))
            {
                entraParams["code_verifier"] = codeVerifier;
            }

            // Confidential client — has client_secret
            if (!string.IsNullOrEmpty(clientSecret))
            {
                entraParams["client_secret"] = clientSecret;
            }

            return await ForwardToEntraAndEnrich(entraParams, clientId, codeVerifier);
        }

        /// <summary>
        /// Handles refresh_token grant.
        /// </summary>
        private async Task<IActionResult> HandleRefreshToken(IFormCollection form)
        {
            var refreshToken = form["refresh_token"].ToString();
            var clientId = form["client_id"].ToString();
            var clientSecret = form["client_secret"].ToString();
            var scope = form["scope"].ToString();

            if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(clientId))
            {
                return BadRequest("Missing required parameters: refresh_token, client_id");
            }

            var entraParams = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId
            };

            if (!string.IsNullOrEmpty(clientSecret))
            {
                entraParams["client_secret"] = clientSecret;
            }

            if (!string.IsNullOrEmpty(scope))
            {
                entraParams["scope"] = SmartScopeMapper.ToEntraFormat(scope, _config.FhirAudience);
            }

            return await ForwardToEntraAndEnrich(entraParams, clientId);
        }

        /// <summary>
        /// Handles client_credentials grant (backend services).
        /// Placeholder — full implementation in Step 5d.
        /// </summary>
        private Task<IActionResult> HandleClientCredentials(IFormCollection form)
        {
            // TODO: Step 5d — JWT assertion validation, convert to client_secret
            _logger.LogWarning("Backend services (client_credentials) not yet implemented");
            return Task.FromResult<IActionResult>(StatusCode(501, "Backend services flow not yet implemented"));
        }

        /// <summary>
        /// Forwards token request to Entra, then enriches the response with SMART fields.
        /// </summary>
        private async Task<IActionResult> ForwardToEntraAndEnrich(
            Dictionary<string, string> entraParams,
            string clientId,
            string? codeVerifier = null)
        {
            var tokenUrl = $"{_config.AuthorityUrl}/token";
            var client = _httpClientFactory.CreateClient();

            // PKCE without secret needs Origin header for CORS
            if (!string.IsNullOrEmpty(codeVerifier) && !entraParams.ContainsKey("client_secret"))
            {
                var host = $"{Request.Scheme}://{Request.Host}";
                client.DefaultRequestHeaders.Add("Origin", host);
            }

            // Forward to Entra
            _logger.LogInformation("Forwarding token request to Entra: {Url}", tokenUrl);
            var entraResponse = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(entraParams));
            var entraBody = await entraResponse.Content.ReadAsStringAsync();

            if (!entraResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Entra token request failed: {Status} {Body}", entraResponse.StatusCode, entraBody);
                return StatusCode((int)entraResponse.StatusCode, entraBody);
            }

            // Parse Entra response
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(entraBody)!;

            // Transform scopes from Entra format to SMART format
            var smartScopes = TransformScopes(tokenResponse);

            // Inject any per-user launch context that the launcher cached under "launch:{oid}"
            // via /auth/context-cache (Strategy A — single path for both EHR launch and React UI).
            EnrichWithLaunchContext(tokenResponse);

            // Extract patient from fhirUser claim
            ExtractPatientFromFhirUser(tokenResponse);

            // Add need_patient_banner if any launch scope present
            if (smartScopes.Any(s => s.StartsWith("launch", StringComparison.OrdinalIgnoreCase)))
            {
                tokenResponse["need_patient_banner"] = JsonSerializer.SerializeToElement(true);
            }

            // Set scope to SMART format
            tokenResponse["scope"] = JsonSerializer.SerializeToElement(string.Join(" ", smartScopes));

            // Return enriched response with cache-control headers
            Response.Headers["Cache-Control"] = "no-store";
            Response.Headers["Pragma"] = "no-cache";

            return Ok(tokenResponse);
        }

        /// <summary>
        /// Transforms scopes from Entra format to SMART format.
        /// </summary>
        private List<string> TransformScopes(Dictionary<string, JsonElement> tokenResponse)
        {
            if (!tokenResponse.TryGetValue("access_token", out var accessTokenElement))
            {
                return new List<string>();
            }

            var accessToken = accessTokenElement.GetString();
            var jwt = new JwtSecurityToken(accessToken);

            // Get scopes from 'scp' claim or 'roles' claim
            var scpClaim = jwt.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
            IEnumerable<string> entraScopes;

            if (!string.IsNullOrEmpty(scpClaim))
            {
                entraScopes = scpClaim.Split(' ');
            }
            else
            {
                entraScopes = jwt.Claims.Where(c => c.Type == "roles").Select(c => c.Value);
            }

            var smartScopes = SmartScopeMapper.ToSmartFormat(entraScopes, _config.FhirAudience);

            // Add openid if id_token present
            if (tokenResponse.ContainsKey("id_token"))
            {
                smartScopes.Add("openid");
            }

            // Add offline_access if refresh_token present
            if (tokenResponse.ContainsKey("refresh_token"))
            {
                smartScopes.Add("offline_access");
            }

            return smartScopes;
        }

        /// <summary>
        /// Enriches token response with cached launch context (patient, encounter).
        /// </summary>
        private void EnrichWithLaunchContext(Dictionary<string, JsonElement> tokenResponse)
        {
            if (!tokenResponse.TryGetValue("access_token", out var accessTokenElement))
                return;

            var accessToken = accessTokenElement.GetString();
            var jwt = new JwtSecurityToken(accessToken);
            var userId = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;

            if (string.IsNullOrEmpty(userId))
                return;

            // Check cache for launch context
            if (_cache.TryGetValue($"launch:{userId}", out Dictionary<string, string>? launchProps) && launchProps != null)
            {
                InjectLaunchContext(tokenResponse, launchProps);
                _cache.Remove($"launch:{userId}");
                _logger.LogInformation("Added launch context for user {UserId}", userId);
            }
        }

        /// <summary>
        /// Writes launch-context key/value pairs (e.g. patient, encounter) into the token response.
        /// </summary>
        private static void InjectLaunchContext(
            Dictionary<string, JsonElement> tokenResponse,
            Dictionary<string, string> launchProps)
        {
            foreach (var prop in launchProps)
            {
                tokenResponse[prop.Key] = JsonSerializer.SerializeToElement(prop.Value);
            }
        }

        /// <summary>
        /// Extracts patient ID from fhirUser claim in id_token.
        /// </summary>
        private void ExtractPatientFromFhirUser(Dictionary<string, JsonElement> tokenResponse)
        {
            if (!tokenResponse.TryGetValue("id_token", out var idTokenElement))
                return;

            var idToken = idTokenElement.GetString();
            if (string.IsNullOrEmpty(idToken))
                return;

            var jwt = new JwtSecurityToken(idToken);
            var fhirUser = jwt.Claims.FirstOrDefault(c => c.Type == "fhirUser")?.Value;

            if (string.IsNullOrEmpty(fhirUser))
                return;

            var parts = fhirUser.Split('/');
            if (parts.Length >= 2)
            {
                var lastTwo = parts.Skip(parts.Length - 2).ToArray();
                if (string.Equals(lastTwo[0], "Patient", StringComparison.OrdinalIgnoreCase))
                {
                    tokenResponse["patient"] = JsonSerializer.SerializeToElement(lastTwo[1]);
                    _logger.LogInformation("Extracted patient {PatientId} from fhirUser", lastTwo[1]);
                }
            }

            tokenResponse["fhirUser"] = JsonSerializer.SerializeToElement(fhirUser);
        }
    }
}
