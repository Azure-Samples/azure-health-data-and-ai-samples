// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
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
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.CurrentCultureIgnoreCase) || context.Request.RequestUri!.LocalPath.Contains("introspection", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            TokenResponse tokenResponse;
            try
            {
                _logger?.LogInformation($"Content String: {context.ContentString}");
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

                if (tokenResponse.TryGetIdToken(out string id_token))
                {
                    _logger?.LogInformation("ID Token found in token response, decoding...");
                    var idTokenClaims = ParseIdTokenClaims(id_token);

                    var idTokenClaim = new ClaimCacheObject
                    {
                        Issuer = idTokenClaims["iss"],
                        Subject = idTokenClaims["sub"]
                    };

                    await _cacheService.SetClaimCacheObjectAsync(idTokenClaims["uti"], idTokenClaim);
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

        private Dictionary<string, string> ParseIdTokenClaims(string token)
        {
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
            {
                throw new ArgumentException("Invalid token format.");
            }

            var jwtToken = handler.ReadJwtToken(token);

            var iss = jwtToken.Issuer;
            var sub = jwtToken.Subject;
            var uti = jwtToken.Claims.FirstOrDefault(c => c.Type == "uti")?.Value;

            if (string.IsNullOrEmpty(iss) || string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(uti))
            {
                throw new SecurityTokenException("One or more required claims are missing (iss, sub, uti).");
            }

            return new Dictionary<string, string>
            {
                { "iss", iss },
                { "sub", sub },
                { "uti", uti }
            };
        }

    }
}
