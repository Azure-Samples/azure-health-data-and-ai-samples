// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AzureHealth.DataServices.Caching.StorageProviders;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// No-op <see cref="ICacheBackingStoreProvider"/> used when distributed caching is not configured.
    /// Lets <c>JsonObjectCache</c> resolve from DI while keeping the cache strictly in-memory
    /// (L1 only). Suitable for single-instance/dev use; for multi-instance Functions, configure
    /// Redis via <c>AZURE_CacheConnectionString</c>.
    /// </summary>
    internal sealed class InMemoryOnlyCacheBackingStoreProvider : ICacheBackingStoreProvider
    {
        public Task AddAsync(string key, object value) => Task.CompletedTask;

        public Task AddAsync<T>(string key, T value) => Task.CompletedTask;

        public Task<T?> GetAsync<T>(string key) => Task.FromResult<T?>(default);

        public Task<string> GetAsync(string key) => Task.FromResult<string>(null!);

        public Task<bool> RemoveAsync(string key) => Task.FromResult(false);
    }
}
