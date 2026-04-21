using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartOnFhirDemo.Infrastructure;
using SmartOnFhirDemo.Services;

namespace SmartOnFhirDemo.Controllers;

public class SmartController : Controller
{
    private readonly AuthService _authService;
    private readonly FhirService _fhirService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly BackendTokenService _backendTokenService;

    public SmartController(
        AuthService authService,
        FhirService fhirService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        BackendTokenService backendTokenService)
    {
        _authService = authService;
        _fhirService = fhirService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _backendTokenService = backendTokenService;
    }

    /// <summary>
    /// Clears the entire server-side session (tokens, PKCE state, FHIR results,
    /// user-context tokens — everything) and expires the session cookie so the
    /// next visit starts with a completely fresh session.
    /// </summary>
    [HttpPost("/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        // Expire the session cookie so the browser discards it immediately.
        var cookieName = ".AspNetCore.Session";
        HttpContext.Response.Cookies.Delete(cookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.Lax
        });

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// SMART Backend Services: server-side <c>client_credentials</c> + <c>private_key_jwt</c> to Okta (no proxy).
    /// Triggered from the single UI when "Backend Services" is selected and Launch is clicked.
    /// </summary>
    [HttpPost("/backend/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> RequestBackendToken(CancellationToken cancellationToken)
    {
        HttpContext.Session.SetString(SessionKeys.LaunchType, Infrastructure.LaunchType.Backend);
        var (result, pretty) = await _backendTokenService.RequestTokenAsync(cancellationToken);

        if (result.IsSuccess && !string.IsNullOrEmpty(pretty))
        {
            HttpContext.Session.Remove(SessionKeys.LastError);
            HttpContext.Session.SetString(SessionKeys.BackendTokenResponseJson, pretty);
            HttpContext.Session.SetString(SessionKeys.BackendAccessToken, result.AccessToken ?? string.Empty);
            HttpContext.Session.SetString(SessionKeys.LastSuccess,
                "Backend Services: access token received from Okta (client_credentials).");
        }
        else
        {
            HttpContext.Session.Remove(SessionKeys.BackendTokenResponseJson);
            HttpContext.Session.Remove(SessionKeys.BackendAccessToken);
            var msg = result.ErrorSummary ?? "Backend token request failed.";
            if (!string.IsNullOrWhiteSpace(result.RawResponseBody))
                msg += " Response: " + result.RawResponseBody;
            HttpContext.Session.SetString(SessionKeys.LastError, msg);
        }

        return RedirectToAction("Index", "Home");
    }

    // Scopes per SMART on FHIR v2 launch type
    private static class DefaultScopes
    {
        public const string Standalone             = "openid fhirUser offline_access launch/patient patient/Patient.rs patient/CarePlan.rs";
        public const string StandaloneConfidential = "openid fhirUser offline_access launch/patient patient/Patient.rs patient/CarePlan.rs";
        public const string Ehr                    = "openid fhirUser offline_access launch patient/Patient.rs patient/CarePlan.rs";
    }

    [HttpGet("/login")]
    public async Task<IActionResult> Login(
        string? launchType,
        string? scope,
        string? launch,
        string? iss,
        CancellationToken cancellationToken)
    {
        launchType ??= Infrastructure.LaunchType.Standalone;

        if (!Infrastructure.LaunchType.IsValid(launchType))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"Unknown launch type: '{launchType}'.");
            return RedirectToAction("Index", "Home");
        }

        if (launchType == Infrastructure.LaunchType.StandaloneConfidential)
        {
            var secret = _configuration["SmartOnFhir:ClientSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                HttpContext.Session.SetString(SessionKeys.LastError,
                    "Confidential Standalone requires SmartOnFhir:ClientSecret to be set in appsettings.json.");
                return RedirectToAction("Index", "Home");
            }
        }

        if (launchType == Infrastructure.LaunchType.Ehr)
        {
            if (string.IsNullOrWhiteSpace(launch))
            {
                HttpContext.Session.SetString(SessionKeys.LastError,
                    "EHR Launch requires a launch token. In a real EHR integration this is supplied by the EHR system.");
                return RedirectToAction("Index", "Home");
            }

            if (string.IsNullOrWhiteSpace(iss))
            {
                HttpContext.Session.SetString(SessionKeys.LastError,
                    "EHR Launch requires an ISS (FHIR base URL). In a real EHR integration this is supplied by the EHR system.");
                return RedirectToAction("Index", "Home");
            }
        }

        scope ??= launchType switch
        {
            Infrastructure.LaunchType.Ehr                    => DefaultScopes.Ehr,
            Infrastructure.LaunchType.StandaloneConfidential => DefaultScopes.StandaloneConfidential,
            _                                                => DefaultScopes.Standalone
        };

        HttpContext.Session.SetString(SessionKeys.LaunchType, launchType);

        if (!string.IsNullOrWhiteSpace(iss))
            HttpContext.Session.SetString(SessionKeys.Iss, iss);
        else
            HttpContext.Session.Remove(SessionKeys.Iss);

        var (redirectUrl, state, codeVerifier) = await _authService.BuildAuthorizationRequestAsync(
            scope, cancellationToken, ehrLaunchToken: launch, iss: iss);

        HttpContext.Session.SetString(SessionKeys.State, state);
        HttpContext.Session.SetString(SessionKeys.CodeVerifier, codeVerifier);

        return Redirect(redirectUrl);
    }

    [HttpGet("/callback")]
    public async Task<IActionResult> Callback(CancellationToken cancellationToken)
    {
        var error            = HttpContext.Request.Query["error"].ToString();
        var errorDescription = HttpContext.Request.Query["error_description"].ToString();
        var code             = HttpContext.Request.Query["code"].ToString();
        var state            = HttpContext.Request.Query["state"].ToString();

        if (!string.IsNullOrEmpty(error))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"Authorization error: {error} - {errorDescription}");
            return RedirectToAction("Index", "Home");
        }

        var originalState = HttpContext.Session.GetString(SessionKeys.State);
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(originalState) || !string.Equals(state, originalState, StringComparison.Ordinal))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "Invalid authorization state returned.");
            return RedirectToAction("Index", "Home");
        }

        var codeVerifier = HttpContext.Session.GetString(SessionKeys.CodeVerifier);
        if (string.IsNullOrEmpty(codeVerifier))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "Missing PKCE code_verifier in session.");
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrEmpty(code))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "Missing authorization code in callback.");
            return RedirectToAction("Index", "Home");
        }

        try
        {
            // Standalone confidential: always send secret.
            // EHR launch: send secret when one is configured — Okta "Web" apps require
            // client_secret at the token endpoint even with PKCE; omitting it causes invalid_client.
            // Standalone public: never send secret (even if appsettings has one for other flows).
            var launchType      = HttpContext.Session.GetString(SessionKeys.LaunchType) ?? string.Empty;
            var hasClientSecret = !string.IsNullOrWhiteSpace(_configuration["SmartOnFhir:ClientSecret"]);
            var useClientSecret = launchType == Infrastructure.LaunchType.StandaloneConfidential
                || (launchType == Infrastructure.LaunchType.Ehr && hasClientSecret);

            var (token, rawTokenJson) = await _authService.ExchangeCodeForTokenAsync(code, codeVerifier, cancellationToken, useClientSecret);

            HttpContext.Session.SetString(SessionKeys.AccessToken, token.AccessToken ?? string.Empty);
            if (!string.IsNullOrEmpty(token.RefreshToken))
                HttpContext.Session.SetString(SessionKeys.RefreshToken, token.RefreshToken);
            HttpContext.Session.SetString(SessionKeys.LastTokenResponseJson, rawTokenJson);

            HttpContext.Session.SetString(SessionKeys.LastSuccess, "Authenticated successfully.");
            HttpContext.Session.Remove(SessionKeys.BackendTokenResponseJson);
        }
        catch (OAuthException ex)
        {
            HttpContext.Session.Remove(SessionKeys.LastTokenResponseJson);
            HttpContext.Session.SetString(SessionKeys.LastError, $"Token error: {ex.Error} - {ex.ErrorDescription}");
        }
        catch (HttpRequestException ex)
        {
            HttpContext.Session.Remove(SessionKeys.LastTokenResponseJson);
            HttpContext.Session.SetString(SessionKeys.LastError, $"HTTP error during token request: {ex.Message}");
        }
        catch (Exception ex)
        {
            HttpContext.Session.Remove(SessionKeys.LastTokenResponseJson);
            HttpContext.Session.SetString(SessionKeys.LastError, $"Unexpected error during token request: {ex.Message}");
        }

        return RedirectToAction("Index", "Home");
    }

    private static readonly HashSet<string> AllowedResourceTypes =
        new(StringComparer.OrdinalIgnoreCase) { "Patient", "Observation", "Condition", "CarePlan", "AllergyIntolerance", "MedicationRequest", "Immunization", "Procedure", "Encounter", "DiagnosticReport" };

    [HttpGet("/fhir")]
    public async Task<IActionResult> FhirResource(string? resource, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resource) || !AllowedResourceTypes.Contains(resource))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"Unsupported resource type: '{resource}'.");
            return RedirectToAction("Index", "Home");
        }

        var accessToken = HttpContext.Session.GetString(SessionKeys.AccessToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "You must login first.");
            return RedirectToAction("Index", "Home");
        }

        // For EHR launch use the FHIR server identified by ISS; otherwise fall back to config.
        var fhirBaseUrl = HttpContext.Session.GetString(SessionKeys.Iss);

        try
        {
            var json = await _fhirService.GetResourceAsync(resource, accessToken, cancellationToken, fhirBaseUrl);
            HttpContext.Session.SetString(SessionKeys.LastFhirResourceJson, json);
            HttpContext.Session.SetString(SessionKeys.LastFhirResourceType, resource);
            HttpContext.Session.SetString(SessionKeys.LastSuccess, $"{resource} resource fetched successfully.");
            return RedirectToAction("Index", "Home", new { show = resource });
        }
        catch (HttpRequestException ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"FHIR {resource} request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"Unexpected error fetching {resource}: {ex.Message}");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet("/backend/fhir")]
    public async Task<IActionResult> BackendFhirResource(string? resource, CancellationToken cancellationToken)
    {
        HttpContext.Session.SetString(SessionKeys.LaunchType, Infrastructure.LaunchType.Backend);
        if (string.IsNullOrWhiteSpace(resource) || !AllowedResourceTypes.Contains(resource))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, $"Unsupported resource type: '{resource}'.");
            return RedirectToAction("Index", "Home");
        }

        var backendToken = HttpContext.Session.GetString(SessionKeys.BackendAccessToken);
        if (string.IsNullOrWhiteSpace(backendToken))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Backend token missing. Run Backend Services launch first.");
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var backendFhirBaseUrl = _configuration["BackendServices:FhirBaseUrl"];
            var json = await _fhirService.GetResourceAsync(resource, backendToken, cancellationToken, backendFhirBaseUrl);
            HttpContext.Session.SetString(SessionKeys.LastFhirResourceJson, json);
            HttpContext.Session.SetString(SessionKeys.LastFhirResourceType, resource);
            HttpContext.Session.SetString(SessionKeys.LastSuccess,
                $"{resource} resource fetched successfully using backend token.");
            return RedirectToAction("Index", "Home", new { show = resource });
        }
        catch (HttpRequestException ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Backend FHIR {resource} request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Unexpected error fetching backend {resource}: {ex.Message}");
        }

        return RedirectToAction("Index", "Home");
    }

    // ── User Context (EHR Simulator identity login) ───────────────────────────

    /// <summary>
    /// Initiates a lightweight OIDC login using the User Context app registration.
    /// Requests only the openid scope — no FHIR audience, no SMART scopes.
    /// The resulting token is used solely to authenticate the context-cache call.
    /// The existing standalone SMART session is not affected.
    /// </summary>
    [HttpGet("/usercontext/login")]
    public async Task<IActionResult> UserContextLogin(CancellationToken cancellationToken)
    {
        HttpContext.Session.SetString(SessionKeys.LaunchType, Infrastructure.LaunchType.Ehr);
        var clientId = _configuration["SmartOnFhir:UserContextClientId"];
        if (string.IsNullOrWhiteSpace(clientId) || clientId.StartsWith("<"))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "SmartOnFhir:UserContextClientId is not configured. Add the User Context app client ID to appsettings.json.");
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var (redirectUrl, state, codeVerifier) =
                await _authService.BuildUserContextAuthorizationRequestAsync(cancellationToken);

            HttpContext.Session.SetString(SessionKeys.UserContextState, state);
            HttpContext.Session.SetString(SessionKeys.UserContextCodeVerifier, codeVerifier);

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Failed to build user context login URL: {ex.Message}");
            return RedirectToAction("Index", "Home");
        }
    }

    /// <summary>
    /// Callback for the User Context OIDC login.
    /// Exchanges the code for a token and stores the access token in session.
    /// Does NOT touch the standalone SMART session tokens.
    /// </summary>
    [HttpGet("/usercontext/callback")]
    public async Task<IActionResult> UserContextCallback(CancellationToken cancellationToken)
    {
        HttpContext.Session.SetString(SessionKeys.LaunchType, Infrastructure.LaunchType.Ehr);
        var error            = HttpContext.Request.Query["error"].ToString();
        var errorDescription = HttpContext.Request.Query["error_description"].ToString();
        var code             = HttpContext.Request.Query["code"].ToString();
        var state            = HttpContext.Request.Query["state"].ToString();

        if (!string.IsNullOrEmpty(error))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"User context login error: {error} - {errorDescription}");
            return RedirectToAction("Index", "Home");
        }

        var originalState = HttpContext.Session.GetString(SessionKeys.UserContextState);
        if (string.IsNullOrEmpty(state) || !string.Equals(state, originalState, StringComparison.Ordinal))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Invalid state in user context callback.");
            return RedirectToAction("Index", "Home");
        }

        var codeVerifier = HttpContext.Session.GetString(SessionKeys.UserContextCodeVerifier);
        if (string.IsNullOrEmpty(codeVerifier) || string.IsNullOrEmpty(code))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Missing code or code_verifier in user context callback.");
            return RedirectToAction("Index", "Home");
        }

        try
        {
            var token = await _authService.ExchangeUserContextCodeAsync(code, codeVerifier, cancellationToken);
            HttpContext.Session.SetString(SessionKeys.UserContextToken, token.AccessToken!);
            HttpContext.Session.SetString(SessionKeys.LastSuccess,
                "EHR user identity confirmed. Now fill in the launch context and click Cache & Launch EHR.");
        }
        catch (OAuthException ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"User context token error: {ex.Error} - {ex.ErrorDescription}");
        }
        catch (Exception ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Unexpected error during user context token exchange: {ex.Message}");
        }

        // Clean up user context OIDC state
        HttpContext.Session.Remove(SessionKeys.UserContextState);
        HttpContext.Session.Remove(SessionKeys.UserContextCodeVerifier);

        return RedirectToAction("Index", "Home");
    }

    // ── EHR Simulator context push ────────────────────────────────────────────

    /// <summary>
    /// Step 1 of the two-step EHR simulator flow:
    ///   - Extracts the current user's sub from the active access token (they must be logged in via standalone first).
    ///   - Pushes patient/encounter context to the proxy's context-cache endpoint using that token as the Bearer.
    ///   - Clears auth tokens from session so the user is prompted to re-authenticate.
    ///   - Redirects into the standard EHR launch flow (/login?launchType=ehr&...).
    /// </summary>
    [HttpPost("/ehr-context-launch")]
    public async Task<IActionResult> EhrContextLaunch(
        [FromForm] string iss,
        [FromForm] string patientId,
        [FromForm] string? encounterId,
        [FromForm] string? scope,
        CancellationToken cancellationToken)
    {
        HttpContext.Session.SetString(SessionKeys.LaunchType, Infrastructure.LaunchType.Ehr);
        // Use the lightweight User Context token (openid-only login) — not the SMART access token.
        var accessToken = HttpContext.Session.GetString(SessionKeys.UserContextToken);
        if (string.IsNullOrEmpty(accessToken))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Please log in as EHR User first (Step 1 of the EHR simulator) before caching context.");
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(iss))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "ISS (FHIR base URL) is required.");
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrWhiteSpace(patientId))
        {
            HttpContext.Session.SetString(SessionKeys.LastError, "Patient ID is required for EHR launch context.");
            return RedirectToAction("Index", "Home");
        }

        // Extract user identifier from the current access token.
        // Must match SmartOnFhir:UserIdClaimType and the proxy's AZURE_UserIdClaimType (same value).
        // For Okta, "uid" is the stable internal id (e.g. 00u11p7hktqOjjgt6698); "sub" is often the login/email.
        // ReadJwtToken does NOT apply InboundClaimTypeMap — claim names are raw JWT names.
        string? userId;
        try
        {
            var handler    = new JwtSecurityTokenHandler();
            var jwt        = handler.ReadJwtToken(accessToken);
            var claimType  = (_configuration["SmartOnFhir:UserIdClaimType"] ?? "uid").Trim();
            if (string.IsNullOrEmpty(claimType))
                claimType = "uid";

            userId = jwt.Claims.FirstOrDefault(c => c.Type == claimType)?.Value
                  ?? jwt.Claims.FirstOrDefault(c => c.Type == "uid")?.Value
                  ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }
        catch
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Could not read the current access token to extract user identity.");
            return RedirectToAction("Index", "Home");
        }

        if (string.IsNullOrEmpty(userId))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "Access token does not contain the configured user id claim (SmartOnFhir:UserIdClaimType).");
            return RedirectToAction("Index", "Home");
        }

        // Build the launch context that the proxy will inject into the EHR token response.
        var launchContext = new Dictionary<string, string> { ["patient"] = patientId.Trim() };
        if (!string.IsNullOrWhiteSpace(encounterId))
            launchContext["encounter"] = encounterId.Trim();

        var launchJson   = JsonSerializer.Serialize(launchContext);
        var launchBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(launchJson));

        // POST to the proxy's context-cache endpoint.
        var contextCacheUrl = _configuration["SmartOnFhir:ContextCacheUrl"];
        if (string.IsNullOrWhiteSpace(contextCacheUrl))
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                "SmartOnFhir:ContextCacheUrl is not configured.");
            return RedirectToAction("Index", "Home");
        }

        var payload     = JsonSerializer.Serialize(new { userId, launch = launchBase64 });
        var client      = _httpClientFactory.CreateClient();
        using var req   = new HttpRequestMessage(HttpMethod.Post, contextCacheUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage cacheResponse;
        try
        {
            cacheResponse = await client.SendAsync(req, cancellationToken);
        }
        catch (Exception ex)
        {
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Failed to reach context-cache endpoint: {ex.Message}");
            return RedirectToAction("Index", "Home");
        }

        if (!cacheResponse.IsSuccessStatusCode)
        {
            var body = await cacheResponse.Content.ReadAsStringAsync(cancellationToken);
            HttpContext.Session.SetString(SessionKeys.LastError,
                $"Context-cache returned {(int)cacheResponse.StatusCode}: {body}");
            return RedirectToAction("Index", "Home");
        }

        // Clear user context token — it has served its purpose.
        // Also clear any existing SMART tokens and FHIR data so the user
        // starts the EHR launch fresh without leftover standalone state.
        HttpContext.Session.Remove(SessionKeys.UserContextToken);
        HttpContext.Session.Remove(SessionKeys.AccessToken);
        HttpContext.Session.Remove(SessionKeys.RefreshToken);
        HttpContext.Session.Remove(SessionKeys.LastTokenResponseJson);
        HttpContext.Session.Remove(SessionKeys.LastFhirResourceJson);
        HttpContext.Session.Remove(SessionKeys.LastFhirResourceType);

        // Use a random opaque value as the launch token passed to Okta.
        // The proxy's TokenOutputFilter looks up context by userId (sub), not this value.
        var launchToken = Guid.NewGuid().ToString("N");

        return RedirectToAction("Login", "Smart", new
        {
            launchType = Infrastructure.LaunchType.Ehr,
            iss        = iss,
            launch     = launchToken,
            scope      = string.IsNullOrWhiteSpace(scope) ? null : scope
        });
    }
}
