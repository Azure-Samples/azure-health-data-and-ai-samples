// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Derives a stable blocklist key for a token. JWT access tokens are keyed by their unique
    /// token id ("uti", or "jti"); opaque tokens (e.g. Entra refresh tokens, which are not JWTs)
    /// are keyed by a SHA-256 hash of the token string. The same derivation is used when blocking a
    /// token and when checking it, so access-token and refresh-token revocation both resolve
    /// consistently.
    /// </summary>
    public static class RevocationKey
    {
        public static string ForToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("token must be provided.", nameof(token));
            }

            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(token))
            {
                // Entra refresh tokens can superficially look like a 3-part JWT (CanReadToken==true)
                // but fail to actually parse. Treat any parse failure as "opaque" and fall through
                // to hashing rather than throwing.
                try
                {
                    var jwt = handler.ReadJwtToken(token);
                    var id = jwt.Claims.FirstOrDefault(c => c.Type == "uti")?.Value
                          ?? jwt.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        return id;
                    }
                }
                catch
                {
                    // Not a real JWT — fall through to hash-based key.
                }
            }

            // Opaque token (or JWT without uti/jti): key by hash of the token string.
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
