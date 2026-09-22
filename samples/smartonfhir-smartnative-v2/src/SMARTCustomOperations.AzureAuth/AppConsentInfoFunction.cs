// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Web;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.ODataErrors;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;
using SMARTCustomOperations.AzureAuth.Strategies;

namespace SMARTCustomOperations.AzureAuth
{
    /// <summary>
    /// Gateway consent picker backend. Reads the requesting SMART app + user's existing
    /// oauth2PermissionGrants via Microsoft Graph and (on POST) narrows the grant based on
    /// the user's selection. Entra IdP only; External mode returns 404.
    /// </summary>
    public class AppConsentInfoFunction
    {
        private const string OidClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private readonly ILogger<AppConsentInfoFunction> _logger;
        private readonly AzureAuthOperationsConfig _config;
        private readonly IIdpStrategy _idpStrategy;
        private readonly IServiceProvider _serviceProvider;

        public AppConsentInfoFunction(
            ILogger<AppConsentInfoFunction> logger,
            AzureAuthOperationsConfig config,
            IIdpStrategy idpStrategy,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _config = config;
            _idpStrategy = idpStrategy;
            _serviceProvider = serviceProvider;
        }

        [Function("AppConsentInfo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "api/appConsentInfo")] HttpRequestData req)
        {
            if (!_idpStrategy.ProvidesConsentPicker)
            {
                return await Text(req, HttpStatusCode.NotFound, "Consent picker is not enabled in this mode.");
            }

            // Services registered only in the EntraId branch; resolve lazily so External mode never touches them.
            var validator = _serviceProvider.GetService(typeof(ContextTokenValidator)) as ContextTokenValidator;
            var consentService = _serviceProvider.GetService(typeof(GraphConsentService)) as GraphConsentService;
            if (validator is null || consentService is null)
            {
                _logger.LogError("Consent picker services not registered. Check IdpType configuration.");
                return await Text(req, HttpStatusCode.InternalServerError, "Consent picker services unavailable.");
            }

            System.Security.Claims.ClaimsPrincipal principal;
            try
            {
                var token = ExtractBearerToken(req);
                principal = await validator.ValidateAsync(token);
            }
            catch (SecurityTokenValidationException ex)
            {
                _logger.LogWarning(ex, "Invalid bearer token on /api/appConsentInfo.");
                return await Text(req, HttpStatusCode.Unauthorized, "Invalid or expired bearer token.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Missing bearer token on /api/appConsentInfo.");
                return await Text(req, HttpStatusCode.Unauthorized, ex.Message);
            }

            var userId = principal.FindFirst(OidClaim)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogError("Token accepted but 'oid' claim missing on /api/appConsentInfo.");
                return await Text(req, HttpStatusCode.Unauthorized, "Token missing 'oid' claim.");
            }

            return string.Equals(req.Method, "POST", StringComparison.OrdinalIgnoreCase)
                ? await HandlePost(req, consentService, userId)
                : await HandleGet(req, consentService, userId);
        }

        private async Task<HttpResponseData> HandleGet(HttpRequestData req, GraphConsentService consentService, string userId)
        {
            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var clientId = query["client_id"];
            var scope = query["scope"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(scope))
            {
                return await Text(req, HttpStatusCode.BadRequest, "Required query parameters missing (client_id, scope).");
            }

            // Convert SMART form ("patient/Condition.rs") to the FHIR Resource App registration form
            // ("patient.Condition.rs") — empty audience skips the wire-format audience prefix.
            var scopes = ScopeFormat.ToEntraFormat(scope!, string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            try
            {
                var info = await consentService.GetAppConsentScopes(clientId!, userId, scopes);
                return await Json(req, HttpStatusCode.OK, info);
            }
            catch (ODataError odataEx)
            {
                return await HandleGraphError(req, odataEx, "GET /api/appConsentInfo");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Bad request on GET /api/appConsentInfo.");
                return await Text(req, HttpStatusCode.BadRequest, ex.Message);
            }
        }

        private async Task<HttpResponseData> HandlePost(HttpRequestData req, GraphConsentService consentService, string userId)
        {
            AppConsentInfo? body;
            try
            {
                var payload = await new StreamReader(req.Body).ReadToEndAsync();
                body = string.IsNullOrWhiteSpace(payload)
                    ? null
                    : JsonConvert.DeserializeObject<AppConsentInfo>(payload);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed JSON on POST /api/appConsentInfo.");
                return await Text(req, HttpStatusCode.BadRequest, "Malformed JSON body.");
            }

            if (body is null || string.IsNullOrWhiteSpace(body.ApplicationId) || body.Scopes is null)
            {
                return await Text(req, HttpStatusCode.BadRequest, "Body must include applicationId and scopes.");
            }

            try
            {
                await consentService.PersistAppConsentScopeIfRemoval(body, userId);
                var okResponse = await Text(req, HttpStatusCode.OK, "Consent updated.");
                AttachPickerDoneCookie(req, okResponse);
                return okResponse;
            }
            catch (ODataError odataEx)
            {
                return await HandleGraphError(req, odataEx, "POST /api/appConsentInfo");
            }
        }

        private async Task<HttpResponseData> HandleGraphError(HttpRequestData req, ODataError error, string operation)
        {
            var code = (int?)error.ResponseStatusCode ?? 500;

            if (code == 403 || string.Equals(error.Error?.Code, "Authorization_RequestDenied", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError(error,
                    "Microsoft Graph denied {Operation}. Ensure the Function App MI has DelegatedPermissionGrant.ReadWrite.All + Application.Read.All granted. Run scripts/Grant-FunctionAppGraphPermissions.ps1.",
                    operation);
                return await Text(req, HttpStatusCode.Forbidden,
                    "Microsoft Graph denied this request. The Function App's managed identity is missing 'DelegatedPermissionGrant.ReadWrite.All' or 'Application.Read.All'. Run scripts/Grant-FunctionAppGraphPermissions.ps1.");
            }

            _logger.LogError(error, "Microsoft Graph error on {Operation}: {Code}", operation, error.Error?.Code);
            return await Text(req, HttpStatusCode.InternalServerError,
                _config.Debug ? $"Microsoft Graph error: {error.Error?.Code} - {error.Error?.Message}" : "Microsoft Graph error.");
        }

        private static string ExtractBearerToken(HttpRequestData req)
        {
            if (!req.Headers.TryGetValues("Authorization", out var values))
            {
                throw new UnauthorizedAccessException("Missing Authorization header.");
            }

            var header = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("Authorization header must be 'Bearer <token>'.");
            }

            return header.Substring("Bearer ".Length).Trim();
        }

        private static async Task<HttpResponseData> Text(HttpRequestData req, HttpStatusCode status, string message)
        {
            var response = req.CreateResponse(status);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            await response.WriteStringAsync(message);
            return response;
        }

        private static async Task<HttpResponseData> Json<T>(HttpRequestData req, HttpStatusCode status, T body)
        {
            var response = req.CreateResponse(status);
            response.Headers.Add("Content-Type", "application/json; charset=utf-8");
            await response.WriteStringAsync(JsonConvert.SerializeObject(body, JsonSettings));
            return response;
        }

        // Short-lived hint cookie so a subsequent /api/authorize skips the picker.
        // Not a security token; the actual scope narrowing was persisted to Entra via Graph.
        private static void AttachPickerDoneCookie(HttpRequestData req, HttpResponseData response)
        {
            var secure = string.Equals(req.Url.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "; Secure" : string.Empty;
            response.Headers.Add(
                "Set-Cookie",
                $"__picker_done=1; Path=/api/; HttpOnly; SameSite=Lax; Max-Age=600{secure}");
        }
    }
}
