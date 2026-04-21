using System.Text.Json;
using OktaSmartBackend.TokenClient;

namespace SmartOnFhirDemo.Services;

/// <summary>
/// Server-side SMART Backend Services: Okta <c>client_credentials</c> + <c>private_key_jwt</c> (OktaM2mClient).
/// </summary>
public sealed class BackendTokenService
{
    private readonly SmartConfigService _smartConfigService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;

    public BackendTokenService(
        SmartConfigService smartConfigService,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _smartConfigService = smartConfigService;
        _configuration = configuration;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Requests an access token using configured <c>BackendServices:*</c>.
    /// Backend Services flow always uses configured scope (typically <c>system/*.rs</c>).
    /// </summary>
    public async Task<(OktaM2mTokenResult Result, string? PrettyJsonForDisplay)> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        var clientId = _configuration["BackendServices:ClientId"];
        var keyId = _configuration["BackendServices:KeyId"];
        var privateKeyRelative = _configuration["BackendServices:PrivateKeyPath"];
        var defaultScope = _configuration["BackendServices:Scope"] ?? "system/*.rs";

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(keyId)
            || string.IsNullOrWhiteSpace(privateKeyRelative))
        {
            return (new OktaM2mTokenResult
            {
                IsSuccess = false,
                ErrorSummary =
                    "BackendServices:ClientId, KeyId, and PrivateKeyPath must be set in configuration."
            }, null);
        }

        var scope = defaultScope;
        var backendFhirBaseUrl = _configuration["BackendServices:FhirBaseUrl"];
        var smartConfig = await _smartConfigService.GetConfigurationAsync(cancellationToken, issUrl: backendFhirBaseUrl);
        var tokenEndpoint = smartConfig.TokenEndpoint;
        if (string.IsNullOrWhiteSpace(tokenEndpoint))
        {
            return (new OktaM2mTokenResult
            {
                IsSuccess = false,
                ErrorSummary = "SMART discovery response is missing token_endpoint."
            }, null);
        }

        var keyPath = Path.IsPathRooted(privateKeyRelative)
            ? privateKeyRelative
            : Path.Combine(_environment.ContentRootPath, privateKeyRelative);

        var http = _httpClientFactory.CreateClient();
        var client = new OktaM2mClient(http);
        var result = await client.RequestAccessTokenFromPemFileAtEndpointAsync(
            keyPath,
            keyId,
            tokenEndpoint,
            clientId,
            scope,
            cancellationToken);

        string? pretty = null;
        if (!string.IsNullOrWhiteSpace(result.RawResponseBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(result.RawResponseBody);
                pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                pretty = result.RawResponseBody;
            }
        }

        return (result, pretty);
    }
}
