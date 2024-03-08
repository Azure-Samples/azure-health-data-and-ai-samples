// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Net;
using System.Security.Claims;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class ContextCacheInputFilter : IInputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly ContextCacheService _cacheService;
        private readonly string _id;
        private readonly string subClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
        private readonly string oidClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

        public ContextCacheInputFilter(ILogger<TokenInputFilter> logger, AzureAuthOperationsConfig configuration, ContextCacheService cacheService)
        {
            _logger = logger;
            _configuration = configuration;
            _cacheService = cacheService;
            _id = Guid.NewGuid().ToString();
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(AppConsentInfoInputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            // Only execute for contextInfo request
            if (!context.Request.RequestUri!.LocalPath.Contains("context-cache", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            // Validate token against Microsoft Graph
            ClaimsPrincipal userPrincipal;
            try
            {
                string token = context.Request.Headers.Authorization!.Parameter!;
                userPrincipal = await _cacheService.ValidateContextAccessTokenAsync(token);
            }
            catch (Exception ex) when (ex is Microsoft.IdentityModel.Tokens.SecurityTokenValidationException || ex is UnauthorizedAccessException)
            {
                _logger?.LogWarning("User attempted to access app consent info without a valid token. {Exception}", ex);
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.Unauthorized);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }
            catch (Exception ex)
            {
                _logger?.LogCritical("Unknown error while validating user token. {Exception}", ex);
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            if (userPrincipal is null || !userPrincipal.HasClaim(x => x.Type == (_configuration.SmartonFhir_with_B2C ? subClaim : oidClaim)))
            {
                _logger?.LogError("User does not have the oid claimin AppConsentInfoInputFilter. {User}", userPrincipal);
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new UnauthorizedAccessException("Token validation failed for get context info operation"), code: HttpStatusCode.Unauthorized);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Parse the request body to the launch cache object
            LaunchCacheObject launchCacheObject;
            try
            {
                launchCacheObject = JsonConvert.DeserializeObject<LaunchCacheObject>(context.ContentString)!;
            }
            catch (Exception ex)
            {
                _logger?.LogError("Invalid body provided for Context Cache endpoint.");
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Ensure the OID provided in the cache body matches the user's access token.
            var userId = userPrincipal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")!.Value;
            if (launchCacheObject.UserId != userId)
            {
                _logger?.LogError($"User provided oid {launchCacheObject.UserId} does not match token oid {userId}.");
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: new UnauthorizedAccessException($"User provided oid {launchCacheObject.UserId} does not match token oid {userId}."), code: HttpStatusCode.Unauthorized);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            // Set the launch cache object in the cache
            await _cacheService.SetLaunchCacheObjectAsync(userId, launchCacheObject);

            // Set a HTTP status code so the binding isn't executed.
            context.ContentString = string.Empty;
            context.StatusCode = HttpStatusCode.NoContent;
            return context;
        }
    }
}
