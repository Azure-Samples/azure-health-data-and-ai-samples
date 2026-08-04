// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// In-memory <c>jti</c> replay cache scoped per backend client.
    /// Note: per-process only; scale out requires a distributed implementation (e.g., Redis).
    /// </summary>
    public sealed class MemoryAssertionReplayProtector : IAssertionReplayProtector
    {
        private readonly IMemoryCache _cache;

        public MemoryAssertionReplayProtector(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryRegister(string clientId, string jti, DateTimeOffset expiresAtUtc)
        {
            var key = $"backend-jti:{clientId}:{jti}";
            if (_cache.TryGetValue(key, out _))
            {
                return false;
            }

            var ttl = expiresAtUtc - DateTimeOffset.UtcNow;
            if (ttl <= TimeSpan.Zero)
            {
                return false;
            }

            // Size=1 is required for the shared MemoryCache SizeLimit (configured in Program.cs)
            // to actually enforce the cap — entries without a Size are never counted.
            _cache.Set(key, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1
            });
            return true;
        }
    }
}
