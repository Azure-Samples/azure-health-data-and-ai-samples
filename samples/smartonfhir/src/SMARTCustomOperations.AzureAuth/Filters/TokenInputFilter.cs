// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
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
    public sealed class TokenInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id;
		private readonly IAuthProvider _authProvider;

		public TokenInputFilter(ILogger<TokenInputFilter> logger, AzureAuthOperationsConfig configuration, IAuthProvider authProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
			_authProvider = authProvider;
		}

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(TokenInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            // Only execute for token request
            if (!context.Request.RequestUri!.LocalPath.Contains("token", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            if (IsRequestUrlFormEncoded(context))
            {
                FilterErrorEventArgs error = new FilterErrorEventArgs(name: Name, id: Id, fatal: true, error: new ArgumentException("Content Type must be application/x-www-form-urlencoded"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Request!.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Read the request body
            TokenContext? tokenContext = null;
            NameValueCollection requestData = await context.Request.Content.ReadAsFormDataAsync();

            // Parse the request body
            try
            {
                tokenContext = TokenContext.FromFormUrlEncodedContent(requestData!, context.Request.Headers.Authorization, _configuration.FhirAudience!);
                tokenContext.Validate();
            }
            catch (Exception)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ArgumentException($"Token request invalid. {tokenContext?.ToLogString() ?? context.ContentString}"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Setup new http client for token request
            try
            {
				// Retrieve OpenID configuration
				var openIdConfig = await _authProvider.GetOpenIdConfigurationAsync(_configuration.SmartonFhir_with_B2C);

				// Access properties from OpenIdConfiguration
				string tokenEndpoint = openIdConfig.TokenEndpoint!;
				context.UpdateRequestUri(context.Request.Method, tokenEndpoint);
				context.Request.Content = tokenContext.ToFormUrlEncodedContent();
			}
            catch (Exception ex)
            {
				FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.BadRequest);
				OnFilterError?.Invoke(this, error);
				return context.SetContextErrorBody(error, _configuration.Debug);
			}            
            
            // Origin header needed for clients using PKCE without a secret (SPA).
            if (requestData.AllKeys.Contains("code_verifier") && !requestData.AllKeys.Contains("client_secret"))
            {
                context.Headers.Add(new HeaderNameValuePair("Origin", $"https://{_configuration.ApiManagementHostName}", CustomHeaderType.RequestStatic));
            }

            return context;
        }

        private bool IsRequestUrlFormEncoded(OperationContext context)
        {
            return context.Request.Content == null ||
                !context.Request.Content.Headers.GetValues("Content-Type")
                .Any(x => string.Equals(x, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
        }
    }
}
