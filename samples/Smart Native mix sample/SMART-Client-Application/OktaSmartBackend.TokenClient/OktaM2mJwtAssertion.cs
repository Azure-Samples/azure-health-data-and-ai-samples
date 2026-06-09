using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace OktaSmartBackend.TokenClient;

/// <summary>
/// Builds a JWT client assertion for Okta <c>private_key_jwt</c> (ES384 / P-384).
/// </summary>
public static class OktaM2mJwtAssertion
{
    public const string ClientAssertionType =
        "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";

    /// <param name="signingKey">ECDSA P-384 private key.</param>
    /// <param name="keyId">Must match the Key ID registered in Okta for this client.</param>
    /// <param name="clientId">OAuth client id (iss and sub).</param>
    /// <param name="tokenEndpointAudience">Full token URL; used as JWT <c>aud</c>.</param>
    public static string Create(
        ECDsa signingKey,
        string keyId,
        string clientId,
        string tokenEndpointAudience)
    {
        if (signingKey == null) throw new ArgumentNullException(nameof(signingKey));
        if (string.IsNullOrWhiteSpace(keyId)) throw new ArgumentException("Key id is required.", nameof(keyId));
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("Client id is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(tokenEndpointAudience))
            throw new ArgumentException("Token endpoint URL is required.", nameof(tokenEndpointAudience));

        var securityKey = new ECDsaSecurityKey(signingKey) { KeyId = keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha384);

        var jti = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: clientId,
            audience: tokenEndpointAudience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, clientId),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            },
            notBefore: now,
            expires: now.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
