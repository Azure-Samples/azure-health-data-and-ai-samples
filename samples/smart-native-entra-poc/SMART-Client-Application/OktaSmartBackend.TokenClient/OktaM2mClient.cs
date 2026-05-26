using System.Security.Cryptography;
using System.Text.Json;

namespace OktaSmartBackend.TokenClient;

/// <summary>
/// Requests an access token using SMART Backend Services style:
/// <c>grant_type=client_credentials</c> + <c>private_key_jwt</c> against Okta.
/// </summary>
public sealed class OktaM2mClient
{
    private readonly HttpClient _http;

    public OktaM2mClient(HttpClient? http = null) => _http = http ?? new HttpClient();

    /// <summary>
    /// Loads an ES384 private key from PEM file and exchanges it for an access token.
    /// </summary>
    /// <param name="privateKeyPemPath">Absolute or relative path to the PEM file.</param>
    public Task<OktaM2mTokenResult> RequestAccessTokenFromPemFileAsync(
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
            return Task.FromResult(new OktaM2mTokenResult
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
    public async Task<OktaM2mTokenResult> RequestAccessTokenAsync(
        ECDsa signingKey,
        string keyId,
        string oktaDomain,
        string authServerId,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var tokenEndpoint = OktaM2mEndpoints.GetTokenEndpoint(oktaDomain, authServerId);
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
    public Task<OktaM2mTokenResult> RequestAccessTokenFromPemFileAtEndpointAsync(
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
            return Task.FromResult(new OktaM2mTokenResult
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
    public async Task<OktaM2mTokenResult> RequestAccessTokenAtEndpointAsync(
        ECDsa signingKey,
        string keyId,
        string tokenEndpoint,
        string clientId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var assertion = OktaM2mJwtAssertion.Create(signingKey, keyId, clientId, tokenEndpoint);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_assertion_type"] = OktaM2mJwtAssertion.ClientAssertionType,
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
            return new OktaM2mTokenResult
            {
                IsSuccess = false,
                ErrorSummary = ex.Message,
                RawResponseBody = ""
            };
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new OktaM2mTokenResult
            {
                IsSuccess = false,
                HttpStatusCode = (int)response.StatusCode,
                ErrorSummary = $"Okta token endpoint returned {(int)response.StatusCode}",
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

            return new OktaM2mTokenResult
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
            return new OktaM2mTokenResult
            {
                IsSuccess = false,
                ErrorSummary = $"Token response parse error: {ex.Message}",
                RawResponseBody = body,
                HttpStatusCode = (int)response.StatusCode
            };
        }
    }
}
