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
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    /// <summary>
    /// Validates the inbound token request and retargets it at the upstream IdP.
    /// External IdP path: forwards as-is to the token_endpoint discovered from FHIR SMART well-known.
    /// Entra path: translates SMART scopes to Entra format and targets Entra v2 token endpoint.
    /// </summary>
    public sealed class TokenInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly IIdpStrategy _idpStrategy;
        private readonly IBackendClientAssertionValidator? _backendValidator;
        private readonly IClientAssertionAuthenticator? _assertionAuthenticator;
        private readonly string _id;

        public TokenInputFilter(
            ILogger<TokenInputFilter> logger,
            AzureAuthOperationsConfig configuration,
            IIdpStrategy idpStrategy,
            IBackendClientAssertionValidator? backendValidator = null,
            IClientAssertionAuthenticator? assertionAuthenticator = null)
        {
            _logger = logger;
            _configuration = configuration;
            _idpStrategy = idpStrategy;
            _backendValidator = backendValidator;
            _assertionAuthenticator = assertionAuthenticator;
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

            // Capture the inbound token URL BEFORE we retarget — used as the expected
            // audience for backend services client_assertion validation (per SMART v2).
            var inboundTokenUri = context.Request.RequestUri!;
            var expectedBackendAudience = inboundTokenUri.IsDefaultPort
                ? $"{inboundTokenUri.Scheme}://{inboundTokenUri.Host}{inboundTokenUri.AbsolutePath}"
                : $"{inboundTokenUri.Scheme}://{inboundTokenUri.Host}:{inboundTokenUri.Port}{inboundTokenUri.AbsolutePath}";

            // Confidential asymmetric client authentication (private_key_jwt) for the user grants
            // (authorization_code, refresh_token). Entra cannot validate a client-registered JWKS,
            // so we validate the assertion here and swap it for the Key Vault client_secret BEFORE
            // classification — the request then flows through the normal confidential-client path
            // (keeping user-consented SMART scopes). The client_credentials backend-services grant
            // keeps its own assertion handling below and is intentionally excluded here.
            if (IsAsymmetricUserGrantRequest(requestData) && _idpStrategy.SupportsBackendServices)
            {
                if (_assertionAuthenticator is null)
                {
                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true,
                        error: new ArgumentException("Asymmetric client authentication is not configured. Set AZURE_BackendServiceKeyVaultStore."),
                        code: HttpStatusCode.BadRequest);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }

                try
                {
                    await _assertionAuthenticator.SwapAssertionForClientSecretAsync(requestData, expectedBackendAudience);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger?.LogWarning("Asymmetric client_assertion rejected: {Message}", ex.Message);
                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.Unauthorized);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Asymmetric client authentication failed unexpectedly.");
                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }
            }

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

            // Reject backend services on IdP strategies that do not support it (e.g. External/Okta).
            if (tokenContext is BackendServiceTokenContext && !_idpStrategy.SupportsBackendServices)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true,
                    error: new ArgumentException("Backend services flow is not supported on this IdP. Use the IdP's native token endpoint."),
                    code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Reject backend services when KV store / validator is not configured.
            if (tokenContext is BackendServiceTokenContext && _backendValidator is null)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true,
                    error: new ArgumentException("Backend services are not configured. Set AZURE_BackendServiceKeyVaultStore."),
                    code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Capture inbound origin BEFORE we retarget the request URI to the upstream IdP.
            // Used below to spoof an Origin header on the Entra public-client (SPA) path so
            // Entra accepts the server-side token redemption (AADSTS9002327).
            string? entraSpaOriginHeader = null;
            if (_idpStrategy.ProvidesAuthorizeProxy && tokenContext is PublicClientTokenContext)
            {
                var inbound = context.Request.RequestUri!;
                entraSpaOriginHeader = inbound.IsDefaultPort
                    ? $"{inbound.Scheme}://{inbound.Host}"
                    : $"{inbound.Scheme}://{inbound.Host}:{inbound.Port}";
            }

            try
            {
                // Strategy decides target token endpoint and scope translation.
                if (_idpStrategy.ProvidesAuthorizeProxy)
                {
                    if (tokenContext is ConfidentialClientTokenContext cc && !string.IsNullOrEmpty(cc.Scope))
                    {
                        cc.Scope = _idpStrategy.TranslateScopesToIdp(cc.Scope!);
                    }
                    else if (tokenContext is PublicClientTokenContext pc && !string.IsNullOrEmpty(pc.Scope))
                    {
                        pc.Scope = _idpStrategy.TranslateScopesToIdp(pc.Scope!);
                    }
                    else if (tokenContext is RefreshTokenContext rc && !string.IsNullOrEmpty(rc.Scope))
                    {
                        rc.Scope = _idpStrategy.TranslateScopesToIdp(rc.Scope!);
                    }
                }

                string tokenEndpointUrl = await _idpStrategy.GetTokenEndpointAsync();

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

            // Backend services: validate the client_assertion locally and swap to the stored
            // Entra client_secret. Entra cannot accept arbitrary JWKS for client_credentials,
            // so the proxy is responsible for asserting trust on the client's behalf.
            if (tokenContext is BackendServiceTokenContext backendContext)
            {
                try
                {
                    var backendClientConfig = await _backendValidator!.ValidateAsync(
                        backendContext.ClientId,
                        backendContext.ClientAssertionType,
                        backendContext.ClientAssertion,
                        expectedBackendAudience);

                    var outboundScope = _idpStrategy.BuildBackendScope(_configuration.FhirAudience ?? string.Empty);
                    context.Request.Content = backendContext.BuildOutboundForm(backendClientConfig.ClientSecret, outboundScope);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger?.LogWarning("Backend services client_assertion rejected: {Message}", ex.Message);
                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.Unauthorized);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Backend services validation failed unexpectedly.");
                    FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                    OnFilterError?.Invoke(this, error);
                    return context.SetContextErrorBody(error, _configuration.Debug);
                }
            }
            else
            {
                context.Request.Content = tokenContext.ToFormUrlEncodedContent();
            }

            // Inject Origin header so Entra accepts the SPA-platform PKCE redemption.
            // Entra-only (ProvidesAuthorizeProxy) and public-client only — external IdP path
            // (e.g. Okta) is left completely untouched.
            if (entraSpaOriginHeader is not null)
            {
                context.Request.Headers.Remove("Origin");
                context.Request.Headers.TryAddWithoutValidation("Origin", entraSpaOriginHeader);
            }

            return context;
        }

        private bool IsNotFormEncoded(OperationContext context)
        {
            return context.Request.Content == null ||
                !context.Request.Content.Headers.GetValues("Content-Type")
                .Any(x => string.Equals(x.Split(";").FirstOrDefault(), "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// True when the request is a user grant (authorization_code or refresh_token) authenticated
        /// with a jwt-bearer client_assertion and no client_secret — i.e. a confidential asymmetric
        /// client. The client_credentials backend-services grant is deliberately excluded; it keeps
        /// its dedicated assertion handling.
        /// </summary>
        private static bool IsAsymmetricUserGrantRequest(NameValueCollection form)
        {
            var grantType = form["grant_type"];
            var isUserGrant =
                string.Equals(grantType, GrantType.authorization_code.ToString(), StringComparison.Ordinal) ||
                string.Equals(grantType, GrantType.refresh_token.ToString(), StringComparison.Ordinal);

            if (!isUserGrant)
            {
                return false;
            }

            var assertionType = form["client_assertion_type"] is { } rawType
                ? Uri.UnescapeDataString(rawType)
                : null;

            return !string.IsNullOrEmpty(form["client_assertion"])
                && string.Equals(assertionType, ClientAssertionAuthenticator.JwtBearerClientAssertionType, StringComparison.Ordinal)
                && string.IsNullOrEmpty(form["client_secret"]);
        }
    }
}
