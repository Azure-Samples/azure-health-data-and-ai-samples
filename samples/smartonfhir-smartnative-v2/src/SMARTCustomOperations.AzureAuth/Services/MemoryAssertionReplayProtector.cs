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
    /// <remarks>
    /// This class owns a dedicated <see cref="MemoryCache"/> instance rather than sharing the
    /// DI-registered <see cref="IMemoryCache"/>. The dedicated instance is bounded via
    /// <see cref="MemoryCacheOptions.SizeLimit"/> so a flood of unique jti values cannot grow
    /// memory unbounded within the assertion TTL window. Keeping the bounded cache private
    /// avoids coupling replay-protection sizing to other <see cref="IMemoryCache"/> consumers
    /// (notably <c>JsonObjectCache</c>, which does not set <c>Size</c> on entries and would
    /// throw on every write if a shared cache had <see cref="MemoryCacheOptions.SizeLimit"/> set).
    /// </remarks>
    public sealed class MemoryAssertionReplayProtector : IAssertionReplayProtector, IDisposable
    {
        private readonly MemoryCache _cache = new(new MemoryCacheOptions
        {
            SizeLimit = 10_000,
            CompactionPercentage = 0.2,
        });

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

            // Size=1 is required by the private cache's SizeLimit — entries without a Size
            // would throw at Set() time.
            _cache.Set(key, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                Size = 1,
            });
            return true;
        }

        public void Dispose() => _cache.Dispose();
    }
}
