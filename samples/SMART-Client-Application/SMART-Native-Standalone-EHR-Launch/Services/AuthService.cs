using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

public class AuthService
{
    private readonly SmartConfigService _smartConfigService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SmartConfigService smartConfigService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _smartConfigService = smartConfigService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Builds the SMART authorize redirect URL.
    /// For EHR launch supply <paramref name="iss"/> so discovery and audience are driven by that URL.
    /// </summary>
    public async Task<(string redirectUrl, string state, string codeVerifier)> BuildAuthorizationRequestAsync(
        string scope,
        CancellationToken cancellationToken,
        string? ehrLaunchToken = null,
        string? iss = null)
    {
        // For EHR launch: discover from iss; for standalone: use configured FhirBaseUrl.
        var smartConfig = await _smartConfigService.GetConfigurationAsync(cancellationToken, issUrl: iss);

        var clientId = _configuration["SmartOnFhir:ClientId"];
        var redirectUri = _configuration["SmartOnFhir:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("SmartOnFhir:ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new InvalidOperationException("SmartOnFhir:RedirectUri is not configured.");

        if (string.IsNullOrWhiteSpace(scope))
            throw new InvalidOperationException("Scope must be provided when initiating authorization.");

        // EHR launch: audience is the ISS the EHR supplied.
        // Standalone: use the configured FhirAudience.
        string? aud;
        if (!string.IsNullOrWhiteSpace(iss))
        {
            aud = iss.TrimEnd('/');
        }
        else
        {
            aud = _configuration["SmartOnFhir:FhirAudience"];
            if (string.IsNullOrWhiteSpace(aud))
                throw new InvalidOperationException("SmartOnFhir:FhirAudience is not configured.");
        }

        var state = GenerateRandomUrlSafeString(32);
        var codeVerifier = GenerateRandomUrlSafeString(64);
        var codeChallenge = CreateCodeChallenge(codeVerifier);

        var queryParams = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["aud"] = aud,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "login consent"
        };

        // EHR launch: include the opaque launch token provided by the EHR.
        if (!string.IsNullOrWhiteSpace(ehrLaunchToken))
            queryParams["launch"] = ehrLaunchToken;

        var authorizationUrl = QueryHelpers.AddQueryString(smartConfig.AuthorizationEndpoint ?? string.Empty, queryParams!);
        return (authorizationUrl, state, codeVerifier);
    }

    /// <summary>
    /// Exchanges the authorization code for tokens via the configured token proxy.
    /// Returns the deserialized token plus the <b>raw JSON</b> from the proxy so the UI can show
    /// SMART-augmented fields (<c>patient</c>, <c>encounter</c>, <c>need_patient_banner</c>, etc.).
    /// </summary>
    public async Task<(TokenResponse token, string rawResponseJson)> ExchangeCodeForTokenAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken,
        bool useClientSecret = false)
    {
        var clientId = _configuration["SmartOnFhir:ClientId"];
        var redirectUri = _configuration["SmartOnFhir:RedirectUri"];
        var clientSecret = useClientSecret ? _configuration["SmartOnFhir:ClientSecret"] : null;

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("SmartOnFhir:ClientId is not configured.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new InvalidOperationException("SmartOnFhir:RedirectUri is not configured.");

        // Use the token_endpoint discovered from the FHIR server's .well-known/smart-configuration.
        var smartConfig = await _smartConfigService.GetConfigurationAsync(cancellationToken);
        var tokenEndpoint = smartConfig.TokenEndpoint;
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
            throw new InvalidOperationException("token_endpoint not found in SMART configuration.");
        _logger.LogInformation("Using token endpoint from SMART configuration: {TokenEndpoint}", tokenEndpoint);

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = redirectUri,
            ["client_id"]     = clientId,
            ["code_verifier"] = codeVerifier
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
            form["client_secret"] = clientSecret;

        var client = _httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(form!);

        var response = await client.PostAsync(tokenEndpoint, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Token endpoint returned error. StatusCode={StatusCode}, Body={Body}", response.StatusCode, json);

            OAuthErrorResponse? error = null;
            try
            {
                error = JsonSerializer.Deserialize<OAuthErrorResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { /* ignore deserialisation error */ }

            if (error != null && !string.IsNullOrEmpty(error.Error))
                throw new OAuthException(error.Error!, error.ErrorDescription ?? "");

            throw new HttpRequestException($"Token endpoint error: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            throw new InvalidOperationException("Token response did not contain an access_token.");

        return (tokenResponse, json);
    }

    // ── User Context (EHR Simulator identity login) ───────────────────────────

    /// <summary>
    /// Builds an OIDC-only authorization URL for the User Context app registration.
    /// Uses openid scope only — no FHIR audience, no SMART scopes.
    /// The resulting token is used solely to authenticate the context-cache call.
    /// </summary>
    public async Task<(string redirectUrl, string state, string codeVerifier)> BuildUserContextAuthorizationRequestAsync(
        CancellationToken cancellationToken)
    {
        // Reuse the same authorization endpoint discovered from the FHIR server.
        var smartConfig = await _smartConfigService.GetConfigurationAsync(cancellationToken);

        var clientId    = _configuration["SmartOnFhir:UserContextClientId"];
        var redirectUri = _configuration["SmartOnFhir:UserContextRedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("SmartOnFhir:UserContextClientId is not configured.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new InvalidOperationException("SmartOnFhir:UserContextRedirectUri is not configured.");

        var state        = GenerateRandomUrlSafeString(32);
        var codeVerifier = GenerateRandomUrlSafeString(64);
        var codeChallenge = CreateCodeChallenge(codeVerifier);

        var queryParams = new Dictionary<string, string>
        {
            ["response_type"]          = "code",
            ["client_id"]              = clientId,
            ["redirect_uri"]           = redirectUri,
            ["scope"]                  = "openid",
            ["state"]                  = state,
            ["code_challenge"]         = codeChallenge,
            ["code_challenge_method"]  = "S256",
            ["prompt"]                 = "login consent"
        };

        var authorizationUrl = QueryHelpers.AddQueryString(smartConfig.AuthorizationEndpoint ?? string.Empty, queryParams!);
        return (authorizationUrl, state, codeVerifier);
    }

    /// <summary>
    /// Exchanges the User Context authorization code for a token.
    /// Uses the User Context app's client id and redirect URI.
    /// </summary>
    public async Task<TokenResponse> ExchangeUserContextCodeAsync(
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var clientId      = _configuration["SmartOnFhir:UserContextClientId"];
        var clientSecret  = _configuration["SmartOnFhir:UserContextClientSecret"];
        var redirectUri   = _configuration["SmartOnFhir:UserContextRedirectUri"];

        // Use the token_endpoint discovered from the FHIR server's .well-known/smart-configuration.
        var smartConfig = await _smartConfigService.GetConfigurationAsync(cancellationToken);
        var tokenEndpoint = smartConfig.TokenEndpoint;

        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("SmartOnFhir:UserContextClientId is not configured.");

        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new InvalidOperationException("SmartOnFhir:UserContextRedirectUri is not configured.");

        if (string.IsNullOrWhiteSpace(tokenEndpoint))
            throw new InvalidOperationException("token_endpoint not found in SMART configuration.");
        _logger.LogInformation("Using user-context token endpoint from SMART configuration: {TokenEndpoint}", tokenEndpoint);

        var form = new Dictionary<string, string>
        {
            ["grant_type"]    = "authorization_code",
            ["code"]          = code,
            ["redirect_uri"]  = redirectUri,
            ["client_id"]     = clientId,
            ["code_verifier"] = codeVerifier
        };

        // Confidential client — include the client secret.
        if (!string.IsNullOrWhiteSpace(clientSecret))
            form["client_secret"] = clientSecret;

        var client = _httpClientFactory.CreateClient();
        using var content = new FormUrlEncodedContent(form!);
        var response = await client.PostAsync(tokenEndpoint, content, cancellationToken);
        var json     = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("User context token endpoint error. StatusCode={StatusCode}, Body={Body}", response.StatusCode, json);

            OAuthErrorResponse? error = null;
            try { error = JsonSerializer.Deserialize<OAuthErrorResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { /* ignore */ }

            if (error != null && !string.IsNullOrEmpty(error.Error))
                throw new OAuthException(error.Error!, error.ErrorDescription ?? "");

            throw new HttpRequestException($"User context token error: {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            throw new InvalidOperationException("User context token response did not contain an access_token.");

        return tokenResponse;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string GenerateRandomUrlSafeString(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", string.Empty);
}

public class OAuthException : Exception
{
    public string Error { get; }
    public string? ErrorDescription { get; }

    public OAuthException(string error, string? errorDescription) : base($"{error}: {errorDescription}")
    {
        Error = error;
        ErrorDescription = errorDescription;
    }
}

public sealed class OAuthErrorResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("error")]
    public string? Error { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
