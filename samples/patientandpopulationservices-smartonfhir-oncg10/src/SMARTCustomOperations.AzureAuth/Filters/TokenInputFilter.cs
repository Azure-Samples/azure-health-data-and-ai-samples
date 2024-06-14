// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Configuration;
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
        private readonly IAsymmetricAuthorizationService _asymmetricAuthorizationService;
        private readonly IAuthProvider _authProvider;

        public TokenInputFilter(ILogger<TokenInputFilter> logger, AzureAuthOperationsConfig configuration, IAsymmetricAuthorizationService asymmetricAuthorizationService, IAuthProvider authProvider)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
            _asymmetricAuthorizationService = asymmetricAuthorizationService;
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
                var openIdConfig = await _authProvider.GetOpenIdConfigurationAsync(_configuration.Authority_URL!);

                // Access properties from OpenIdConfiguration
                string tokenEndpointUrl = openIdConfig.TokenEndpoint!;

                int splitIndex = tokenEndpointUrl.IndexOf('/', tokenEndpointUrl.IndexOf("//") + 2);

                // Split the URL into two parts
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

            // Microsoft Entra ID does not support bare JWKS auth or 384 JWKS auth. We must convert to an associated client secret flow.
            if (tokenContext.GetType() == typeof(BackendServiceTokenContext))
            {
                var castTokenContext = (BackendServiceTokenContext)tokenContext;
                context = await HandleBackendService(context, castTokenContext);
            }
            else
            {
                context.Request.Content = tokenContext.ToFormUrlEncodedContent();
            }

            // Origin header needed for clients using PKCE without a secret (SPA).
            if (requestData.AllKeys.Contains("code_verifier") && !requestData.AllKeys.Contains("client_secret"))
            {
                context.Headers.Add(new HeaderNameValuePair("Origin", $"https://{_configuration.ApiManagementHostName}", CustomHeaderType.RequestStatic));
            }

            return context;
        }

        private async Task<OperationContext> HandleBackendService(OperationContext context, BackendServiceTokenContext castTokenContext)
        {
            // Check client id to see if it exists. Get JWKS.
            BackendClientConfiguration? clientConfig = null;

            // Fetch the jwks for this client and validate

            try
            {
                clientConfig = await _asymmetricAuthorizationService.AuthenticateBackendAsyncClient(castTokenContext.ClientId, castTokenContext.ClientAssertion);
            }
            catch (HttpRequestException)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ConfigurationErrorsException($"JWKS url {clientConfig?.JwksUri?.ToString()} is not responding. Please check the client configuration."));
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }
            catch (Exception ex) when (ex is Microsoft.IdentityModel.Tokens.SecurityTokenValidationException || ex is UnauthorizedAccessException)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.Unauthorized);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Request.Content = castTokenContext.ConvertToClientCredentialsFormUrlEncodedContent(clientConfig.ClientSecret);

            return context;
        }

        private bool IsRequestUrlFormEncoded(OperationContext context)
        {
            return context.Request.Content == null ||
                !context.Request.Content.Headers.GetValues("Content-Type")
                .Any(x => string.Equals(x.Split(";").FirstOrDefault(), "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
        }
    }
}
