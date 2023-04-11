// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;

#pragma warning disable CA1002 // Do not expose generic lists

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class GraphConsentService : BaseContextAppService
    {
        private readonly ILogger<BaseContextAppService> _logger;
        private readonly GraphServiceClient _graphServiceClient;
        private readonly Dictionary<string, ServicePrincipal> _resourceServicePrincipals = new();

        public GraphConsentService(AzureAuthOperationsConfig configuration, ILogger<BaseContextAppService> logger) : base(configuration, logger)
        {
            _graphServiceClient = new GraphServiceClient(new DefaultAzureCredential());
            _logger = logger;
        }

        public async Task<AppConsentInfo> GetAppConsentScopes(string requestingAppClientId, string userId, string[] requestedScopes)
        {
            // Get needed objects from graph
            var requestingClientApp = await GetRequestingApplication(requestingAppClientId);
            var resourceServicePrincipals = await GetResourceServicePrincipals(requestingClientApp.RequiredResourceAccess.Select(x => x.ResourceAppId).Distinct());
            var requestingServicePrincipal = await GetRequestingServicePrincipal(requestingAppClientId);
            var permissions = await GetUserAppOAuth2PermissinGrants(requestingServicePrincipal.Id, userId);

            AppConsentInfo info = new();
            info.ApplicationId = requestingClientApp.AppId;
            info.ApplicationName = requestingClientApp.DisplayName;
            info.ApplicationDescription = requestingClientApp.Description;
            info.ApplicationUrl = requestingClientApp.Info.MarketingUrl;

            var requestedAndApprovedScopes = string.Join(" ", permissions.Select(x => x.Scope)).Split(" ").Union(requestedScopes);

            foreach (string scope in requestedAndApprovedScopes)
            {
                var requestingClientAppScopeIds = GetAppScopeIds(requestingClientApp);
                var matchingResourcePrincipal = resourceServicePrincipals.Values.Where(x => x.Oauth2PermissionScopes.Any(y => requestingClientAppScopeIds.Contains((Guid)y.Id!) && y.Value == scope)).FirstOrDefault();
                var scopeInfo = matchingResourcePrincipal?.Oauth2PermissionScopes.Where(x => x.Value == scope).FirstOrDefault();
                var scopeConsentRecord = permissions.SingleOrDefault(x => x.ResourceId == matchingResourcePrincipal?.Id && x.Scope.Contains(scope, StringComparison.InvariantCultureIgnoreCase));

                if (matchingResourcePrincipal is not null && scopeInfo?.Id is not null)
                {
                    info.Scopes.Add(new AppConsentScope()
                    {
                        Name = scopeInfo.Value,
                        Id = scopeInfo.Id.ToString(),
                        ResourceId = matchingResourcePrincipal?.Id,
                        Consented = scopeConsentRecord is not null,
                        ConsentId = scopeConsentRecord?.Id,
                        UserDescription = scopeInfo.UserConsentDescription,
                    });
                }
            }

            return info;
        }

        public async Task PersistAppConsentScopeIfRemoval(AppConsentInfo consentInfo, string userId)
        {
            // Task.WhenAll could be used for better performance.
            // NOTE! This is not immediate! The client should check that the scopes persisted then sleep for like 30 seconds.

            foreach (var resourceId in consentInfo.Scopes.Where(x => x is not null).Select(x => x.ResourceId).Distinct())
            {
                var resourceScopes = consentInfo.Scopes.Where(x => x.ResourceId == resourceId).ToList();
                var scopeString = string.Join(" ", resourceScopes.Where(x => x.Consented).Select(x => x.Name));
                if (resourceScopes.Any(x => x.ConsentId is not null))
                {
                    var consentId = resourceScopes.First(x => x.ConsentId is not null).ConsentId!;
                    await UpdateUserAppOAuth2PermissionGrantIfRemovalNeeded(consentId, scopeString);
                }

                /*
                Below is not needed. Graph will prompt user for scopes if needed

                else if (resourceScopes.Any())
                {
                    var requestingServicePrincipal = await GetRequestingServicePrincipal(consentInfo.ApplicationId!);
                    await CreateUserAppOAuth2PermissinGrant(requestingServicePrincipal.Id, userId, resourceId!, scopeString);
                }*/
            }

            return;
        }

        public async Task DeleteAppConsentScope(AppConsentInfo consentInfo, string userId)
        {
            foreach (var grantId in consentInfo.Scopes.Select(x => x.ConsentId).Distinct())
            {
                if (grantId is not null)
                {
                    await DeleteUserAppOAuth2PermissionGrant(grantId);
                }
            }
        }

        private async Task<Application> GetRequestingApplication(string applicationId)
        {
            var requestingApp = await _graphServiceClient.Applications.Request().Filter($"appId eq '{applicationId}'").GetAsync();

            if (requestingApp.Count != 1)
            {
                throw new ArgumentException($"Could not find single application for app id {applicationId}");
            }

            return requestingApp.First();
        }

        private async Task<ServicePrincipal> GetRequestingServicePrincipal(string applicationId)
        {
            var requestingServicePrincipal = await _graphServiceClient.ServicePrincipals.Request().Filter($"appId eq '{applicationId}'").GetAsync();

            if (requestingServicePrincipal.Count != 1)
            {
                throw new ArgumentException($"Could not find single Service Principal for app id {applicationId}");
            }

            return requestingServicePrincipal.First();
        }

        private static List<Guid> GetAppScopeIds(Application clientApp)
        {
            List<Guid> appScopeIds = new();

            foreach (var resource in clientApp.RequiredResourceAccess)
            {
                foreach (var scope in resource.ResourceAccess)
                {
                    if (scope?.Type == "Scope" && scope?.Id is not null)
                    {
                        appScopeIds.Add((Guid)scope.Id);
                    }
                }
            }

            return appScopeIds;
        }

        private async Task<Dictionary<string, ServicePrincipal>> GetResourceServicePrincipals(IEnumerable<string> resourceAppIds)
        {
            foreach (var resourceAppId in resourceAppIds)
            {
                if (_resourceServicePrincipals.ContainsKey(resourceAppId))
                {
                    continue;
                }

                var servicePrincipal = await _graphServiceClient.ServicePrincipals.Request().Filter($"appId eq '{resourceAppId}'").GetAsync();

                if (servicePrincipal.Count != 1)
                {
                    throw new ArgumentException($"Could not find single service principal for resource app id {resourceAppId}");
                }

                _resourceServicePrincipals.Add(resourceAppId, servicePrincipal.First());
            }

            return _resourceServicePrincipals;
        }

        private async Task<List<OAuth2PermissionGrant>> GetUserAppOAuth2PermissinGrants(string requestingAppClientId, string userId)
        {
            var permissionPage = await _graphServiceClient.Oauth2PermissionGrants.Request().Filter($"clientId eq '{requestingAppClientId}' and consentType eq 'Principal' and principalId eq '{userId}'").GetAsync();
            return permissionPage.ToList();
        }

        private async Task<OAuth2PermissionGrant> CreateUserAppOAuth2PermissinGrant(string servicePrincipalId, string userId, string resourceId, string scope)
        {
            var permission = new OAuth2PermissionGrant
            {
                ClientId = servicePrincipalId,
                ConsentType = "Principal",
                PrincipalId = userId,
                ResourceId = resourceId,
                Scope = scope,
            };

            return await _graphServiceClient.Oauth2PermissionGrants.Request().AddAsync(permission);
        }

        private async Task UpdateUserAppOAuth2PermissionGrantIfRemovalNeeded(string grantId, string scope)
        {
            var existingGrant = await _graphServiceClient.Oauth2PermissionGrants[grantId].Request().GetAsync();
            var existingScopes = existingGrant.Scope.Split(" ");
            var newScopes = scope.Split(" ");

            // We only care about removing scopes, not adding them. Graph will consent for the others.
            if (existingScopes.Except(newScopes).Any())
            {
                _logger.LogInformation($"Updating OAuth2PermissionGrant {grantId} with scope {scope}");

                /*
                var permission = new OAuth2PermissionGrant
                {
                    Scope = scope,
                };
                await _graphServiceClient.Oauth2PermissionGrants[grantId].Request().UpdateAsync(permission);
                */

                await _graphServiceClient.Oauth2PermissionGrants[grantId].Request().DeleteAsync();
            }

            return;
        }

        private async Task DeleteUserAppOAuth2PermissionGrant(string grantId)
        {
            _logger.LogInformation($"Deleting OAuth2PermissionGrant {grantId}");
            await _graphServiceClient.Oauth2PermissionGrants[grantId].Request().DeleteAsync();
            return;
        }
    }
}
