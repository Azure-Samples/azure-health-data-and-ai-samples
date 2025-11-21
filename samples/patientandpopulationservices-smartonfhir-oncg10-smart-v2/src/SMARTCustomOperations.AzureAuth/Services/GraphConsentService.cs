// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class GraphConsentService : BaseContextAppService
    {
        private readonly ILogger<BaseContextAppService> _logger;
        private readonly GraphServiceClient _graphServiceClient;
        private readonly Dictionary<string, ServicePrincipal> _resourceServicePrincipals = new();

        public GraphConsentService(AzureAuthOperationsConfig configuration, GraphServiceClient graphServiceClient, ILogger<BaseContextAppService> logger) : base(configuration, logger)
        {
            _graphServiceClient = graphServiceClient;
            _logger = logger;
        }

        public async Task<AppConsentInfo> GetAppConsentScopes(string requestingAppClientId, string userId, string[] requestedScopes)
        {
            // Get needed objects from graph
            var requestingClientApp = await GetRequestingApplication(requestingAppClientId);
            var resourceServicePrincipals = await GetResourceServicePrincipals(requestingClientApp.RequiredResourceAccess!.Select(x => x.ResourceAppId!).Distinct());
            var requestingServicePrincipal = await GetRequestingServicePrincipal(requestingAppClientId);
            var permissions = await GetUserAppOAuth2PermissionGrants(requestingServicePrincipal.Id!, userId);

            AppConsentInfo info = new()
            {
                ApplicationId = requestingClientApp.AppId,
                ApplicationName = requestingClientApp.DisplayName,
                ApplicationDescription = requestingClientApp.Description,
                ApplicationUrl = requestingClientApp.Info?.MarketingUrl ?? string.Empty
            };

            var requestedAndApprovedScopes = string.Join(" ", permissions.Select(x => x.Scope)).Split(" ").Union(requestedScopes);

            var graphAppId = "00000003-0000-0000-c000-000000000000";

            var allSpScopes = resourceServicePrincipals?.Values?
                                .Where(sp =>
                                    sp.AppId != graphAppId &&                  // exclude MS Graph
                                    sp.Oauth2PermissionScopes != null)         // ensure scopes exist
                                .SelectMany(sp => sp.Oauth2PermissionScopes!)
                                .ToList()
                                ?? new List<PermissionScope>();

            var requestedApprovedAndGranularScopes = new HashSet<string>(requestedAndApprovedScopes, StringComparer.OrdinalIgnoreCase);

            foreach (var reqScope in requestedAndApprovedScopes)
            {
                if (reqScope.Equals("launch", StringComparison.OrdinalIgnoreCase))
                {
                    requestedApprovedAndGranularScopes.Add(reqScope);
                    continue;
                }
                foreach (var spScope in allSpScopes)
                {
                    var spRoot = GetScopeRoot(spScope.Value!);

                    if (spRoot.Equals(reqScope, StringComparison.OrdinalIgnoreCase))
                    {
                        requestedApprovedAndGranularScopes.Add(spScope.Value!);
                    }
                }
            }

            _logger.LogInformation("Adding AppConsent Scope");
            foreach (string scope in requestedApprovedAndGranularScopes)
            {
                var requestingClientAppScopeIds = GetAppScopeIds(requestingClientApp);
                var matchingResourcePrincipal = resourceServicePrincipals.Values.Where(x => x.Oauth2PermissionScopes!.Any(y => requestingClientAppScopeIds.Contains((Guid)y.Id!) && y.Value == scope)).FirstOrDefault();
                var scopeInfo = matchingResourcePrincipal?.Oauth2PermissionScopes!.Where(x => x.Value == scope).FirstOrDefault();
                var scopeConsentRecord = permissions.SingleOrDefault(x => x.ResourceId == matchingResourcePrincipal?.Id && x.Scope!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)));

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

        private static string GetScopeRoot(string scope)
        {
            var idx = scope.IndexOf('?');
            return idx < 0 ? scope : scope.Substring(0, idx);
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
                    await CreateUserAppOAuth2PermissionGrant(requestingServicePrincipal.Id, userId, resourceId!, scopeString);
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
            var requestingApp = await _graphServiceClient.Applications.GetAsync((rq) =>
            {
                rq.QueryParameters.Filter = $"appId eq '{applicationId}'";
            });

            if (requestingApp!.Value!.Count != 1)
            {
                throw new ArgumentException($"Could not find single application for app id {applicationId}");
            }

            return requestingApp.Value.Single();
        }

        private async Task<ServicePrincipal> GetRequestingServicePrincipal(string applicationId)
        {
            var requestingServicePrincipal = await _graphServiceClient.ServicePrincipals.GetAsync((rq) =>
            {
                rq.QueryParameters.Filter = $"appId eq '{applicationId}'";
            });


            if (requestingServicePrincipal!.Value!.Count != 1)
            {
                throw new ArgumentException($"Could not find single Service Principal for app id {applicationId}");
            }

            return requestingServicePrincipal.Value.Single();
        }

        private static List<Guid> GetAppScopeIds(Application clientApp)
        {
            List<Guid> appScopeIds = new();

            foreach (var resource in clientApp.RequiredResourceAccess!)
            {
                foreach (var scope in resource.ResourceAccess!)
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

                var servicePrincipal = await _graphServiceClient.ServicePrincipals.GetAsync((rq) =>
                {
                    rq.QueryParameters.Filter = $"appId eq '{resourceAppId}'";
                });

                if (servicePrincipal!.Value!.Count != 1)
                {
                    throw new ArgumentException($"Could not find single service principal for resource app id {resourceAppId}");
                }

                _resourceServicePrincipals.Add(resourceAppId, servicePrincipal.Value.Single());
            }

            return _resourceServicePrincipals;
        }

        private async Task<List<OAuth2PermissionGrant>> GetUserAppOAuth2PermissionGrants(string requestingAppClientId, string userId)
        {
            var permissionPage = await _graphServiceClient.Oauth2PermissionGrants.GetAsync((rq) =>
            {
                rq.QueryParameters.Filter = $"clientId eq '{requestingAppClientId}' and consentType eq 'Principal' and principalId eq '{userId}'";
            });

            return permissionPage!.Value!.ToList();
        }

        private async Task<OAuth2PermissionGrant> CreateUserAppOAuth2PermissionGrant(string servicePrincipalId, string userId, string resourceId, string scope)
        {
            var permission = new OAuth2PermissionGrant
            {
                ClientId = servicePrincipalId,
                ConsentType = "Principal",
                PrincipalId = userId,
                ResourceId = resourceId,
                Scope = scope,
            };

            return (await _graphServiceClient.Oauth2PermissionGrants.PostAsync(permission))!;
        }

        private async Task UpdateUserAppOAuth2PermissionGrantIfRemovalNeeded(string grantId, string scope)
        {
            var existingGrant = await _graphServiceClient.Oauth2PermissionGrants[grantId].GetAsync();
            var existingScopes = existingGrant!.Scope!.Split(" ");
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

                await _graphServiceClient.Oauth2PermissionGrants[grantId].DeleteAsync();
            }

            return;
        }

        private async Task DeleteUserAppOAuth2PermissionGrant(string grantId)
        {
            _logger.LogInformation($"Deleting OAuth2PermissionGrant {grantId}");
            await _graphServiceClient.Oauth2PermissionGrants[grantId].DeleteAsync();
            return;
        }
    }
}
