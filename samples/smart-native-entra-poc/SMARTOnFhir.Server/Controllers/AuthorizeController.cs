using System.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SMARTOnFhir.Server.Configuration;
using SMARTOnFhir.Server.Services;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class AuthorizeController : ControllerBase
    {
        private readonly SmartConfig _config;
        private readonly ILogger<AuthorizeController> _logger;

        public AuthorizeController(IOptions<SmartConfig> config, ILogger<AuthorizeController> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        [HttpGet("/auth/authorize")]
        public IActionResult AuthorizeGet()
        {
            return HandleAuthorize(Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString()));
        }

        [HttpPost("/auth/authorize")]
        public async Task<IActionResult> AuthorizePost()
        {
            var form = await Request.ReadFormAsync();
            var parameters = form.ToDictionary(f => f.Key, f => f.Value.ToString());
            return HandleAuthorize(parameters);
        }

        private IActionResult HandleAuthorize(Dictionary<string, string> parameters)
        {
            // Extract SMART parameters
            parameters.TryGetValue("response_type", out var responseType);
            parameters.TryGetValue("client_id", out var clientId);
            parameters.TryGetValue("scope", out var scope);
            parameters.TryGetValue("aud", out var aud);
            parameters.TryGetValue("redirect_uri", out var redirectUri);
            parameters.TryGetValue("state", out var state);
            parameters.TryGetValue("launch", out var launch);
            parameters.TryGetValue("user", out var user);
            parameters.TryGetValue("code_challenge", out var codeChallenge);
            parameters.TryGetValue("code_challenge_method", out var codeChallengeMethod);
            parameters.TryGetValue("prompt", out var prompt);
            parameters.TryGetValue("login_hint", out var loginHint);

            // 1. Classify FHIR-access vs. pure-OIDC.
            //    A SMART scope ("launch", "launch/*", "patient/*", "user/*", "system/*") implies the
            //    caller will eventually access the FHIR API — so "aud" is mandatory and validated.
            //    Pure-OIDC requests (e.g. the client app's User Context login with scope=openid)
            //    do NOT need "aud" — they only authenticate the user and obtain a token that will
            //    be used as Bearer credentials at /auth/context-cache.
            var requiresFhirAud = HasFhirScope(scope!);

            if (requiresFhirAud && string.IsNullOrEmpty(aud))
            {
                return BadRequest("Required parameter missing: aud (required when SMART/FHIR scopes are requested).");
            }

            if (!string.IsNullOrEmpty(aud))
            {
                var providedAudience = HttpUtility.UrlDecode(aud).TrimEnd('/');
                var validAudiences = new[]
                {
                    _config.FhirAudience.TrimEnd('/'),
                    _config.FhirServerUrl.TrimEnd('/')
                };

                if (!validAudiences.Any(a => string.Equals(a, providedAudience, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogError("Invalid audience. Got: {Got}, Valid: {Valid}", providedAudience, string.Join(", ", validAudiences));
                    return BadRequest("Invalid audience");
                }
            }

            // 2. Validate required parameters (aud handled above based on scope)
            if (string.IsNullOrEmpty(responseType) || string.IsNullOrEmpty(clientId) ||
                string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(redirectUri) ||
                string.IsNullOrEmpty(state))
            {
                return BadRequest("Required parameters missing (response_type, client_id, scope, redirect_uri, state)");
            }

            // 3. Validate response_type
            if (!string.Equals(responseType, "code", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest($"Invalid response_type: {responseType}. Only 'code' is supported.");
            }

            // 4. Classify the request per SMART App Launch v2:
            //      - EHR launch: a non-empty "launch" parameter is present. The token is OPAQUE to
            //        the SMART app per spec and to us as proxy — context is resolved server-side at
            //        /auth/token time by EnrichWithLaunchContext, which reads the per-user
            //        "launch:{oid}" cache entry that the launcher populated via /auth/context-cache
            //        before initiating this flow.
            //      - Standalone with launch scope: no "launch" parameter, but "launch", "launch/patient",
            //        or "launch/encounter" appears in the requested scope. The auth server may show
            //        a context-selection UI when UseConsentUI=true.
            //      - Standalone without launch scope: pure direct pass-through to Entra.
            //      - Pure OIDC (e.g. client-app User Context login, scope=openid): also pass through.
            var isEhrLaunch = !string.IsNullOrEmpty(launch);
            var hasStandaloneLaunchScope = !isEhrLaunch && HasLaunchScope(scope!);
            var hasUserSentinel = string.Equals(user, "true", StringComparison.OrdinalIgnoreCase);

            // 4a. Standalone launch needing context selection — redirect to the React consent UI when enabled.
            //     The UI handles scope confirmation (and optional patient/encounter selection), then re-issues
            //     the authorize request with user=true so we proceed directly to Entra on the second pass.
            //     EHR launches NEVER come through here, by design — the launcher has already cached context.
            if (hasStandaloneLaunchScope && _config.UseConsentUI && !hasUserSentinel)
            {
                _logger.LogInformation("Standalone launch with launch scope, UI enabled: redirecting to consent UI");
                var qs = Request.QueryString.Value;
                if (Request.Method == "POST")
                {
                    qs = "?" + string.Join("&", parameters.Select(p =>
                        $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}"));
                }
                return Redirect($"{Request.Scheme}://{Request.Host}/auth/context/{qs}");
            }

            // 5. Transform SMART scopes to Entra format
            var entraScopes = SmartScopeMapper.ToEntraFormat(scope, _config.FhirAudience);

            // 6. Build Entra authorize URL — same path for all flows: pass through with SMART app's
            //    real redirect_uri and state. For EHR launches we forward "launch" opaquely as well.
            var authorizeUrl = $"{_config.AuthorityUrl}/authorize";
            var entraRedirectUri = redirectUri!;
            var entraState = state!;

            if (isEhrLaunch)
            {
                _logger.LogInformation("EHR launch: forwarding opaque launch token to Entra. Context lookup happens at /auth/token time via launch:{{oid}} cache.");
            }

            var queryParams = new List<string>
            {
                $"response_type={HttpUtility.UrlEncode(responseType)}",
                $"client_id={HttpUtility.UrlEncode(clientId)}",
                $"scope={HttpUtility.UrlEncode(entraScopes)}",
                $"redirect_uri={HttpUtility.UrlEncode(entraRedirectUri)}",
                $"state={HttpUtility.UrlEncode(entraState)}"
            };

            if (!string.IsNullOrEmpty(aud))
            {
                queryParams.Add($"aud={HttpUtility.UrlEncode(aud)}");
            }

            if (!string.IsNullOrEmpty(codeChallenge))
                queryParams.Add($"code_challenge={HttpUtility.UrlEncode(codeChallenge)}");
            if (!string.IsNullOrEmpty(codeChallengeMethod))
                queryParams.Add($"code_challenge_method={HttpUtility.UrlEncode(codeChallengeMethod)}");
            if (!string.IsNullOrEmpty(prompt))
            {
                // Entra only accepts single prompt values: login, consent, none, select_account
                // SMART apps may send "login consent" — use "consent" which covers both
                var promptValue = prompt.Contains("consent", StringComparison.OrdinalIgnoreCase) ? "consent" : prompt.Split(' ')[0];
                queryParams.Add($"prompt={HttpUtility.UrlEncode(promptValue)}");
            }
            if (!string.IsNullOrEmpty(loginHint))
                queryParams.Add($"login_hint={HttpUtility.UrlEncode(loginHint)}");

            var entraRedirectUrl = $"{authorizeUrl}?{string.Join("&", queryParams)}";

            _logger.LogInformation("Redirecting to Entra authorize endpoint (EHR launch: {IsEhr})", isEhrLaunch);

            // 7. Redirect to Entra
            return Redirect(entraRedirectUrl);
        }

        /// <summary>
        /// True if the requested scope set contains any standalone-launch context scope
        /// ("launch", "launch/patient", "launch/encounter", etc.). These signal that the auth
        /// server should allow the user to identify the patient/encounter for standalone flows.
        /// </summary>
        private static bool HasLaunchScope(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }

            return scope.Replace('+', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s.Equals("launch", StringComparison.OrdinalIgnoreCase)
                       || s.StartsWith("launch/", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True if the requested scope set contains any SMART scope that implies FHIR API access:
        /// launch context scopes, or resource scopes prefixed with patient/, user/, or system/.
        /// When true, "aud" must be supplied and validated. When false (e.g. pure OIDC login such
        /// as the client app's User Context flow with scope=openid), "aud" is not required.
        /// </summary>
        private static bool HasFhirScope(string scope)
        {
            if (string.IsNullOrEmpty(scope))
            {
                return false;
            }

            return scope.Replace('+', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(s =>
                    s.Equals("launch", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("launch/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("patient/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("system/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
