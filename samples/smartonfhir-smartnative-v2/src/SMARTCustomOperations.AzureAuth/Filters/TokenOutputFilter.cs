// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    /// <summary>
    /// Augments the IdP's token response with SMART-specific fields (patient, fhirUser, launch context).
    /// In Entra mode, also back-translates scopes from Entra format to SMART format.
    /// External-IdP behavior is unchanged.
    /// </summary>
    public sealed class TokenOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly IIdpStrategy _idpStrategy;
        private readonly ContextCacheService? _cacheService;
        private readonly string _id;

        public TokenOutputFilter(ILogger<TokenOutputFilter> logger, AzureAuthOperationsConfig configuration, IIdpStrategy idpStrategy, ContextCacheService? cacheService = null)
        {
            _logger = logger;
            _configuration = configuration;
            _idpStrategy = idpStrategy;
            _cacheService = cacheService;
            _id = Guid.NewGuid().ToString();
        }

#pragma warning disable CS0067
        public event EventHandler<FilterErrorEventArgs>? OnFilterError;
#pragma warning restore CS0067

        public string Name => nameof(TokenOutputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.CurrentCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            TokenResponse tokenResponse;
            try
            {
                Func<IEnumerable<string>, IEnumerable<string>>? scopeBackTranslator =
                    _idpStrategy.ProvidesAuthorizeProxy ? _idpStrategy.TranslateScopesFromIdp : null;

                tokenResponse = new(context.ContentString, _idpStrategy.UserIdClaimType, scopeBackTranslator);

                bool isEhrLaunch = tokenResponse.Scopes.Any(s => s == "launch");

                if (isEhrLaunch && tokenResponse.UserId is not null && _cacheService is not null)
                {
                    try
                    {
                        var cachedLaunchInfo = await _cacheService.GetLaunchCacheObjectAsync(tokenResponse.UserId);
                        if (cachedLaunchInfo?.LaunchProperties is not null)
                        {
                            foreach (var launchProperty in cachedLaunchInfo.LaunchProperties)
                            {
                                tokenResponse.AddCustomProperty(launchProperty.Key, launchProperty.Value);
                            }

                            await _cacheService.RemoveLaunchCacheObjectAsync(tokenResponse.UserId);
                        }
                        else if (_configuration.Debug)
                        {
                            _logger?.LogWarning("No launch information found in cache for user {UserId}", tokenResponse.UserId);
                        }
                    }
                    catch (Exception cacheEx)
                    {
                        _logger?.LogWarning("Failed to retrieve launch context from cache for user {UserId}. Continuing without launch context. {Exception}", tokenResponse.UserId, cacheEx);
                    }
                }

                context.ContentString = tokenResponse.ToString();
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Headers.Add(new HeaderNameValuePair("Cache-Control", "no-store", CustomHeaderType.ResponseStatic));
            context.Headers.Add(new HeaderNameValuePair("Pragma", "no-cache", CustomHeaderType.ResponseStatic));

            return context;
        }
    }
}
