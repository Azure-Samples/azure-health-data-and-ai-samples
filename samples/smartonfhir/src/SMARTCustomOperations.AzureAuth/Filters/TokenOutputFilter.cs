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

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class TokenOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly ContextCacheService _cacheService;
        private readonly string _id;

        public TokenOutputFilter(ILogger<TokenOutputFilter> logger, AzureAuthOperationsConfig configuration, ContextCacheService cacheService)
        {
            _logger = logger;
            _configuration = configuration;
            _cacheService = cacheService;
            _id = Guid.NewGuid().ToString();
        }

#pragma warning disable CS0067 // Needed to implement interface.
        public event EventHandler<FilterErrorEventArgs>? OnFilterError;
#pragma warning restore CS0067 // Needed to implement interface.

        public string Name => nameof(AuthorizeInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            // Only execute for token request
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.CurrentCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            TokenResponse tokenResponse;
            try
            {
                tokenResponse = new(_configuration, context.ContentString);
                
                // Add launch information from cache if exists
                if (tokenResponse.UserId is not null)
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
                        _logger?.LogWarning($"No launch information found in cache for user {tokenResponse.UserId}");
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

            await Task.CompletedTask;
            return context;
        }
    }
}
