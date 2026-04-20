// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    /// <summary>
    /// For external IDPs (Okta, Ping, etc.) that support SMART scopes natively,
    /// this filter simply validates and forwards the token request to the IDP's token endpoint.
    /// No scope translation is needed.
    /// </summary>
    public sealed class TokenInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly FhirSmartConfigService _smartConfigService;
        private readonly string _id;

        public TokenInputFilter(ILogger<TokenInputFilter> logger, AzureAuthOperationsConfig configuration, FhirSmartConfigService smartConfigService)
        {
            _logger = logger;
            _configuration = configuration;
            _smartConfigService = smartConfigService;
            _id = Guid.NewGuid().ToString();
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(TokenInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            if (IsNotFormEncoded(context))
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ArgumentException("Content Type must be application/x-www-form-urlencoded"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Request!.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            NameValueCollection requestData = await context.Request.Content.ReadAsFormDataAsync();

            TokenContext? tokenContext = null;
            try
            {
                tokenContext = TokenContext.FromFormUrlEncodedContent(requestData!, context.Request.Headers.Authorization);
                tokenContext.Validate();
            }
            catch (Exception)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ArgumentException($"Token request invalid. {tokenContext?.ToLogString() ?? context.ContentString}"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            try
            {
                string tokenEndpointUrl = await _smartConfigService.GetTokenEndpointAsync();

                int splitIndex = tokenEndpointUrl.IndexOf('/', tokenEndpointUrl.IndexOf("//") + 2);
                string tokenEndpoint = tokenEndpointUrl.Substring(0, splitIndex + 1);
                string tokenPath = tokenEndpointUrl.Substring(splitIndex + 1);

                context.UpdateRequestUri(context.Request.Method, tokenEndpoint, tokenPath);
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Request.Content = tokenContext.ToFormUrlEncodedContent();

            return context;
        }

        private bool IsNotFormEncoded(OperationContext context)
        {
            return context.Request.Content == null ||
                !context.Request.Content.Headers.GetValues("Content-Type")
                .Any(x => string.Equals(x.Split(";").FirstOrDefault(), "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
        }
    }
}
