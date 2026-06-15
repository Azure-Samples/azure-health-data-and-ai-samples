using System.Security.Cryptography;
using System.Text.Json;

namespace SmartBackendServices.TokenClient;

/// <summary>
/// Requests an access token using SMART v2 Backend Services:
/// <c>grant_type=client_credentials</c> + <c>private_key_jwt</c>.
/// IdP-agnostic — supply the token endpoint discovered from the FHIR SMART well-known metadata.
/// </summary>
public sealed class SmartBackendTokenClient
{
    private readonly HttpClient _http;

    public SmartBackendTokenClient(HttpClient? http = null) => _http = http ?? new HttpClient();

    /// <summary>
    /// Loads an ES384 private key from PEM file and exchanges it for an access token.
    /// </summary>
    /// <param name="privateKeyPemPath">Absolute or relative path to the PEM file.</param>
    public Task<SmartBackendTokenResult> RequestAccessTokenFromPemFileAsync(
        string privateKeyPemPath,
        string keyId,
        string oktaDomain,
        string authServerId,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPemPath))
            throw new ArgumentException("Private key path is required.", nameof(privateKeyPemPath));

        if (!File.Exists(privateKeyPemPath))
        {
            return Task.FromResult(new SmartBackendTokenResult
            {
                IsSuccess = false,
                ErrorSummary = $"Private key file not found: {privateKeyPemPath}",
                RawResponseBody = ""
            });
        }

        var pem = File.ReadAllText(privateKeyPemPath);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);

        return RequestAccessTokenAsync(ecdsa, keyId, oktaDomain, authServerId, clientId, scope, cancellationToken);
    }

    /// <summary>Uses an in-memory <see cref="ECDsa"/> key (already loaded).</summary>
    public async Task<SmartBackendTokenResult> RequestAccessTokenAsync(
        ECDsa signingKey,
        string keyId,
        string oktaDomain,
        string authServerId,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = SmartBackendEndpoints.GetOktaTokenEndpoint(oktaDomain, authServerId);
        return await RequestAccessTokenAtEndpointAsync(
            signingKey,
            keyId,
            tokenEndpoint,
            clientId,
            scope,
            cancellationToken);
    }

    /// <summary>
    /// Loads key from PEM and requests token at an explicitly provided token endpoint.
    /// Useful for SMART discovery-driven flows that use <c>token_endpoint</c> from well-known metadata.
    /// </summary>
    public Task<SmartBackendTokenResult> RequestAccessTokenFromPemFileAtEndpointAsync(
        string privateKeyPemPath,
        string keyId,
        string tokenEndpoint,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(privateKeyPemPath))
            throw new ArgumentException("Private key path is required.", nameof(privateKeyPemPath));

        if (!File.Exists(privateKeyPemPath))
        {
            return Task.FromResult(new SmartBackendTokenResult
            {
                IsSuccess = false,
                ErrorSummary = $"Private key file not found: {privateKeyPemPath}",
                RawResponseBody = ""
            });
        }

        var pem = File.ReadAllText(privateKeyPemPath);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);

        return RequestAccessTokenAtEndpointAsync(ecdsa, keyId, tokenEndpoint, clientId, scope, cancellationToken);
    }

    /// <summary>
    /// Uses an in-memory key and an explicit OAuth token endpoint.
    /// </summary>
    public async Task<SmartBackendTokenResult> RequestAccessTokenAtEndpointAsync(
        ECDsa signingKey,
        string keyId,
        string tokenEndpoint,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var assertion = SmartBackendJwtAssertion.Create(signingKey, keyId, clientId, tokenEndpoint);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_assertion_type"] = SmartBackendJwtAssertion.ClientAssertionType,
            ["client_assertion"] = assertion,
            ["scope"] = scope
        };

        using var content = new FormUrlEncodedContent(form);
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(tokenEndpoint, content, cancellationToken);
        }
        catch (Exception ex)
        {
            return new SmartBackendTokenResult
            {
                IsSuccess = false,
                ErrorSummary = ex.Message,
                RawResponseBody = ""
            };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new SmartBackendTokenResult
            {
                IsSuccess = false,
                HttpStatusCode = (int)response.StatusCode,
                ErrorSummary = $"Token endpoint returned {(int)response.StatusCode}",
                RawResponseBody = body
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var accessToken = root.GetProperty("access_token").GetString();
            int? expiresIn = root.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : null;
            string? tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";

            return new SmartBackendTokenResult
            {
                IsSuccess = true,
                AccessToken = accessToken,
                ExpiresIn = expiresIn,
                TokenType = tokenType,
                RawResponseBody = body,
                HttpStatusCode = (int)response.StatusCode
            };
        }
        catch (Exception ex)
        {
            return new SmartBackendTokenResult
            {
                IsSuccess = false,
                ErrorSummary = $"Token response parse error: {ex.Message}",
                RawResponseBody = body,
                HttpStatusCode = (int)response.StatusCode
            };
        }
    }
}
