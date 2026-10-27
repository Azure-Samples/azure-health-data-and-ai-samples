// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth
{
    /// <summary>
    /// Revokes (blocklists) an access token. The caller POSTs the raw access token in the request
    /// body; the gateway extracts its unique token id (Entra "uti", or "jti" fallback) and records
    /// it in the shared blocklist. Subsequent FHIR requests presenting the token are rejected with
    /// 401 by the FHIR proxy.
    ///
    /// Microsoft Entra cannot natively revoke a self-contained access token, so this gateway-side
    /// blocklist provides the revocation behavior required by the SMART/ONC token revocation test.
    /// Possession of the token is treated as authorization to revoke it (same model as the
    /// reference sample's APIM block-access-token endpoint).
    /// </summary>
    public class BlockAccessTokenFunction
    {
        private readonly ILogger<BlockAccessTokenFunction> _logger;
        private readonly ITokenBlocklistService _blocklist;

        public BlockAccessTokenFunction(ILogger<BlockAccessTokenFunction> logger, ITokenBlocklistService blocklist)
        {
            _logger = logger;
            _blocklist = blocklist;
        }

        [Function("BlockAccessToken")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/block-access-token")] HttpRequestData req)
        {
            var token = (await new StreamReader(req.Body).ReadToEndAsync())?.Trim();

            // Allow the token to arrive either as the raw body or as a bare "Bearer <token>".
            if (!string.IsNullOrEmpty(token) && token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Request body must contain the access token to revoke.");
                return bad;
            }

            // Access tokens are JWTs (keyed by uti/jti); refresh tokens are opaque (keyed by hash).
            // RevocationKey handles both so either token type can be revoked via this endpoint.
            string tokenId;
            try
            {
                tokenId = RevocationKey.ForToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not derive a revocation key for the provided token.");
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Could not derive a revocation key for the provided token.");
                return bad;
            }

            await _blocklist.BlockAsync(tokenId);
            _logger.LogInformation("Token revoked (revocation key {TokenId}).", tokenId);

            // 204 No Content — matches the reference sample's revocation response.
            return req.CreateResponse(HttpStatusCode.NoContent);
        }
    }
}
