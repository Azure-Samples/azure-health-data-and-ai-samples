// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AzureHealth.DataServices.Caching;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;

#pragma warning disable CA1002 // Do not expose generic lists

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class ContextCacheService : BaseContextAppService
    {
        private readonly ILogger<BaseContextAppService> _logger;
        private readonly bool _debug;
        private readonly string _contextAppClientId;
        private readonly IJsonObjectCache _cache;

        public ContextCacheService(AzureAuthOperationsConfig configuration, IJsonObjectCache cache, ILogger<BaseContextAppService> logger) : base(configuration, logger)
        {
            _logger = logger;
            _debug = configuration.Debug;
            _contextAppClientId = configuration.ContextAppClientId!;
            _cache = cache;
        }

        public async Task<LaunchCacheObject> GetLaunchCacheObjectAsync(string key)
        {
            return await _cache.GetAsync<LaunchCacheObject>(key);
        }

        public async Task SetLaunchCacheObjectAsync(string key, LaunchCacheObject item)
        {
            await _cache.AddAsync<LaunchCacheObject>(key, item);
        }

        public async Task RemoveLaunchCacheObjectAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }
    }
}
