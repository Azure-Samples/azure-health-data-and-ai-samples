// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Text.Json;
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
    public class AuthorizeInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id = Guid.NewGuid().ToString();
		private readonly IAuthProvider _authProvider;

		public AuthorizeInputFilter(ILogger<AuthorizeInputFilter> logger, AzureAuthOperationsConfig configuration, IAuthProvider authProvider)
        {
            _logger = logger;
            _configuration = configuration;
			_authProvider = authProvider;
		}

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(AuthorizeInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            // Only execute for authorize request
            if (!context.Request!.RequestUri!.LocalPath.Contains("authorize", StringComparison.CurrentCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            // Get and parse SMART launch params from the request
            AuthorizeContext launchContext;
            try
            {
                launchContext = await ParseLaunchContext(context.Request, _configuration.FhirAudience!);
            }
            catch (ArgumentException ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Verify response_type is "code"
            if (!launchContext.IsValidResponseType())
            {
                // default error event in WebPipeline.cs of toolkit
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ArgumentException($"Invalid response type  {launchContext.ResponseType}"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Verify required SMART launch params are not null
            if (!launchContext.Validate())
            {
                // default error event in WebPipeline.cs of toolkit
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new ArgumentException($"Required launch parameters missing. {JsonSerializer.Serialize(launchContext)}"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

			//Build authorize url

			try
			{
				// Retrieve OpenID configuration
				var openIdConfig = await _authProvider.GetOpenIdConfigurationAsync(_configuration.B2C_Authority_URL!);

				// Access properties from OpenIdConfiguration
				var redirectUrl = openIdConfig.AuthorizationEndpoint;
				var redirect_querystring = launchContext.ToRedirectQueryString();
				var newRedirectUrl = $"{redirectUrl}?{redirect_querystring}";

				context.StatusCode = HttpStatusCode.Redirect;
				context.Headers.Add(new HeaderNameValuePair("Location", newRedirectUrl, CustomHeaderType.ResponseStatic));

				context.Request.RequestUri = new Uri(newRedirectUrl);

				await Task.CompletedTask;
			}
			catch (Exception ex)
			{
				FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.BadRequest);
				OnFilterError?.Invoke(this, error);
				return context.SetContextErrorBody(error, _configuration.Debug);
			}            

            return context;
        }

        private static async Task<AuthorizeContext> ParseLaunchContext(HttpRequestMessage req, string audience)
        {
            if (req.Method == HttpMethod.Post)
            {
                if (req.Content!.Headers.GetValues("Content-Type").Single().Contains("application/x-www-form-urlencoded", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (req.Content is null)
                    {
                        throw new ArgumentException("Body must contain data");
                    }

                    return new AuthorizeContext(await req.Content.ReadAsFormDataAsync()).Translate(audience);
                }
                else
                {
                    throw new ArgumentException("Unsupported Content-Type");
                }
            }
            else if (req.Method == HttpMethod.Get)
            {
                return new AuthorizeContext(req.RequestUri.ParseQueryString()).Translate(audience);
            }
            else
            {
                throw new ArgumentException("Unsupported HTTP method");
            }
        }
    }
}
