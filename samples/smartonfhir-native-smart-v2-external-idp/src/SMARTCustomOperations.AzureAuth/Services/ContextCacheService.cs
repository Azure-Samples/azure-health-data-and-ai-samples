// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Security.Claims;
using Microsoft.AzureHealth.DataServices.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;
using System.IdentityModel.Tokens.Jwt;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class ContextCacheService
    {
        private readonly ILogger<ContextCacheService> _logger;
        private readonly IJsonObjectCache _cache;
        private readonly AzureAuthOperationsConfig _config;
        private readonly FhirSmartConfigService _smartConfigService;

        public ContextCacheService(AzureAuthOperationsConfig configuration, IJsonObjectCache cache, ILogger<ContextCacheService> logger, FhirSmartConfigService smartConfigService)
        {
            _logger = logger;
            _cache = cache;
            _config = configuration;
            _smartConfigService = smartConfigService;
        }

        public async Task<LaunchCacheObject> GetLaunchCacheObjectAsync(string key)
        {
            return await _cache.GetAsync<LaunchCacheObject>(key);
        }

        public async Task SetLaunchCacheObjectAsync(string key, LaunchCacheObject item)
        {
            await _cache.AddAsync(key, item);
        }

        public async Task RemoveLaunchCacheObjectAsync(string key)
        {
            await _cache.RemoveAsync(key);
        }

        public async Task<ClaimCacheObject> GetClaimCacheObjectAsync(string key)
        {
            return await _cache.GetAsync<ClaimCacheObject>(key);
        }

        public async Task SetClaimCacheObjectAsync(string key, ClaimCacheObject item)
        {
            await _cache.AddAsync(key, item);
        }

        public async Task<ClaimsPrincipal> ValidateContextAccessTokenAsync(string token)
        {
            var authorityUrl = (await _smartConfigService.GetAuthorityUrlAsync()).TrimEnd('/');
            var openIdConfigUrl = $"{authorityUrl}/.well-known/openid-configuration";

            var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                openIdConfigUrl,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            var openIdConfig = await configManager.GetConfigurationAsync();

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = openIdConfig.Issuer,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKeys = openIdConfig.SigningKeys,
            };

            if (!string.IsNullOrEmpty(_config.ContextAppClientId))
            {
                validationParameters.ValidateAudience = true;
                validationParameters.ValidAudience = _config.ContextAppClientId;
            }

            // Disable the default claim type remapping so that JWT claim names (e.g. "sub")
            // are preserved as-is on the ClaimsPrincipal instead of being mapped to long
            // CLR URIs (e.g. http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier).
            var handler = new JwtSecurityTokenHandler
            {
                InboundClaimTypeMap = new Dictionary<string, string>()
            };
            return handler.ValidateToken(token, validationParameters, out _);
        }
    }
}
