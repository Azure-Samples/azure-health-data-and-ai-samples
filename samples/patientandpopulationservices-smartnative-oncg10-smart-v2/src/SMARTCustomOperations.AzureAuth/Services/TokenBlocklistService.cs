// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AzureHealth.DataServices.Caching;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Cache-backed token blocklist. Reuses the same <see cref="IJsonObjectCache"/> as the EHR
    /// launch context cache, so it is in-memory by default and distributed (across instances and
    /// restarts) when a Redis backing store is configured via AZURE_CacheConnectionString.
    /// For reliable revocation on a multi-instance deployment, configure Redis.
    /// </summary>
    public sealed class TokenBlocklistService : ITokenBlocklistService
    {
        private const string KeyPrefix = "revoked-token:";

        private readonly IJsonObjectCache _cache;
        private readonly ILogger<TokenBlocklistService> _logger;

        public TokenBlocklistService(IJsonObjectCache cache, ILogger<TokenBlocklistService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task BlockAsync(string tokenId)
        {
            if (string.IsNullOrWhiteSpace(tokenId))
            {
                throw new ArgumentException("tokenId must be provided.", nameof(tokenId));
            }

            await _cache.AddAsync(KeyPrefix + tokenId, new BlockedTokenCacheObject());
            _logger.LogInformation("Access token {TokenId} added to the revocation blocklist.", tokenId);
        }

        public async Task<bool> IsBlockedAsync(string tokenId)
        {
            if (string.IsNullOrWhiteSpace(tokenId))
            {
                return false;
            }

            try
            {
                var entry = await _cache.GetAsync<BlockedTokenCacheObject>(KeyPrefix + tokenId);
                return entry is not null && entry.Blocked;
            }
            catch (Exception ex)
            {
                // Fail open on cache errors (do not block legitimate traffic on a cache hiccup),
                // but log so operators can see it.
                _logger.LogWarning(ex, "Blocklist lookup failed for token {TokenId}; treating as not blocked.", tokenId);
                return false;
            }
        }
    }
}
