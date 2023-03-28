// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class TokenOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id;

        public TokenOutputFilter(ILogger<TokenOutputFilter> logger, AzureAuthOperationsConfig configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
        }

#pragma warning disable CS0067 // Needed to implement interface.
        public event EventHandler<FilterErrorEventArgs>? OnFilterError;
#pragma warning restore CS0067 // Needed to implement interface.

        public string Name => nameof(AuthorizeInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        string IFilter.Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            // Only execute for token request
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.CurrentCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            
            TokenResponse tokenResponse = new(_configuration, context.ContentString, GetLaunchInformation(context.Request.Headers));
            context.ContentString = tokenResponse.ToString();

            if (tokenResponse.FhirUser is not null)
            {
                context.Headers.Add(new HeaderNameValuePair("x-ms-fhirUser", tokenResponse.FhirUser, CustomHeaderType.ResponseStatic));
                context.Headers.Add(new HeaderNameValuePair("Set-Cookie", $"fhirUser={tokenResponse.FhirUser}; path=/; HttpOnly", CustomHeaderType.ResponseStatic));
            }

            context.Headers.Add(new HeaderNameValuePair("Cache-Control", "no-store", CustomHeaderType.ResponseStatic));
            context.Headers.Add(new HeaderNameValuePair("Pragma", "no-cache", CustomHeaderType.ResponseStatic));

            await Task.CompletedTask;
            return context;
        }

        private static Dictionary<string, object>? GetLaunchInformation(HttpRequestHeaders headers)       
        {
            var cookies = headers.GetCookies();
            var launch = cookies.FirstOrDefault(x => x.Cookies.Any(x => x.Name == "launch"))?.Cookies?.FirstOrDefault(x => x.Name == "launch")?.Value;

            var launchDecoded = launch.DecodeBase64();
            if (launchDecoded is not null)
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(launchDecoded);
            }

            return null;
        }
    }
}
