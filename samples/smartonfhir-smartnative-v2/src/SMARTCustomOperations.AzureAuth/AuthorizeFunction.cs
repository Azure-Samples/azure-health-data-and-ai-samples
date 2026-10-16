// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth
{
    /// <summary>
    /// Gateway authorize endpoint used by Entra mode. Validates SMART parameters,
    /// translates scopes via the IdP strategy, and 302-redirects to the upstream
    /// authorize endpoint. In External mode this function is a no-op (404) because
    /// clients call the IdP /authorize directly per the well-known config.
    /// </summary>
    public class AuthorizeFunction
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _config;
        private readonly IIdpStrategy _idpStrategy;

        public AuthorizeFunction(ILogger<AuthorizeFunction> logger, AzureAuthOperationsConfig config, IIdpStrategy idpStrategy)
        {
            _logger = logger;
            _config = config;
            _idpStrategy = idpStrategy;
        }

        [Function("Authorize")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/authorize")] HttpRequestData req)
        {
            if (!_idpStrategy.ProvidesAuthorizeProxy)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Authorize proxy is not enabled in this mode.");
                return notFound;
            }

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            string? Get(string k) => query[k];

            var responseType = Get("response_type");
            var clientId = Get("client_id");
            var scope = Get("scope");
            var aud = Get("aud");
            var redirectUri = Get("redirect_uri");
            var state = Get("state");
            var launch = Get("launch");
            var codeChallenge = Get("code_challenge");
            var codeChallengeMethod = Get("code_challenge_method");
            var prompt = Get("prompt");
            var loginHint = Get("login_hint");

            // Required SMART parameters
            if (string.IsNullOrEmpty(responseType) || string.IsNullOrEmpty(clientId)
                || string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(redirectUri)
                || string.IsNullOrEmpty(state))
            {
                return await BadRequest(req, "Required parameters missing (response_type, client_id, scope, redirect_uri, state).");
            }

            if (!string.Equals(responseType, "code", StringComparison.OrdinalIgnoreCase))
            {
                return await BadRequest(req, $"Invalid response_type: {responseType}. Only 'code' is supported.");
            }

            // aud is required when SMART/FHIR scopes are requested.
            var requiresFhirAud = HasFhirScope(scope!);
            if (requiresFhirAud && string.IsNullOrEmpty(aud))
            {
                return await BadRequest(req, "Required parameter missing: aud (required when SMART/FHIR scopes are requested).");
            }

            if (!string.IsNullOrEmpty(aud))
            {
                var providedAudience = HttpUtility.UrlDecode(aud).TrimEnd('/');
                var validAudiences = new[]
                {
                    (_config.FhirAudience ?? string.Empty).TrimEnd('/'),
                    (_config.FhirServerUrl ?? string.Empty).TrimEnd('/'),
                };

                if (!validAudiences.Any(a => !string.IsNullOrEmpty(a) && string.Equals(a, providedAudience, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogError("Invalid audience. Got: {Got}, Valid: {Valid}", providedAudience, string.Join(", ", validAudiences));
                    return await BadRequest(req, "Invalid audience.");
                }
            }

            var entraScopes = _idpStrategy.TranslateScopesToIdp(scope!);
            var authorizeBase = await _idpStrategy.GetAuthorizeEndpointAsync();

            var queryParams = new List<string>
            {
                $"response_type={HttpUtility.UrlEncode(responseType)}",
                $"client_id={HttpUtility.UrlEncode(clientId)}",
                $"scope={HttpUtility.UrlEncode(entraScopes)}",
                $"redirect_uri={HttpUtility.UrlEncode(redirectUri)}",
                $"state={HttpUtility.UrlEncode(state)}",
            };

            if (!string.IsNullOrEmpty(aud))
            {
                queryParams.Add($"aud={HttpUtility.UrlEncode(aud)}");
            }
            if (!string.IsNullOrEmpty(codeChallenge))
            {
                queryParams.Add($"code_challenge={HttpUtility.UrlEncode(codeChallenge)}");
            }
            if (!string.IsNullOrEmpty(codeChallengeMethod))
            {
                queryParams.Add($"code_challenge_method={HttpUtility.UrlEncode(codeChallengeMethod)}");
            }
            if (!string.IsNullOrEmpty(prompt))
            {
                // Entra accepts only one of: login | consent | none | select_account.
                // SMART apps occasionally send "login consent" — collapse to "consent".
                var promptValue = prompt.Contains("consent", StringComparison.OrdinalIgnoreCase)
                    ? "consent"
                    : prompt.Split(' ')[0];
                queryParams.Add($"prompt={HttpUtility.UrlEncode(promptValue)}");
            }
            if (!string.IsNullOrEmpty(loginHint))
            {
                queryParams.Add($"login_hint={HttpUtility.UrlEncode(loginHint)}");
            }

            var location = $"{authorizeBase}?{string.Join("&", queryParams)}";
            _logger.LogInformation("Redirecting authorize to upstream IdP. EHR launch: {IsEhr}", !string.IsNullOrEmpty(launch));

            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", location);
            return response;
        }

        private static async Task<HttpResponseData> BadRequest(HttpRequestData req, string message)
        {
            var resp = req.CreateResponse(HttpStatusCode.BadRequest);
            await resp.WriteStringAsync(message);
            return resp;
        }

        /// <summary>
        /// True if any requested scope implies FHIR API access (launch, patient/, user/, system/).
        /// Pure-OIDC requests (e.g. scope=openid) do not require aud.
        /// </summary>
        private static bool HasFhirScope(string scope)
        {
            return scope.Replace('+', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(s =>
                    s.Equals("launch", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("launch/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("patient/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("user/", StringComparison.OrdinalIgnoreCase)
                    || s.StartsWith("system/", StringComparison.OrdinalIgnoreCase));
        }
    }
}
