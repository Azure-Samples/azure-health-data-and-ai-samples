// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Reads and rewrites a user's delegated permission grants for the consent picker.
    /// Entra IdP only. Called by /api/appConsentInfo (Phase 2).
    /// </summary>
    public class GraphConsentService
    {
        private readonly ILogger<GraphConsentService> _logger;
        private readonly GraphServiceClient _graphServiceClient;
        private readonly Dictionary<string, ServicePrincipal> _resourceServicePrincipals = new();

        public GraphConsentService(GraphServiceClient graphServiceClient, ILogger<GraphConsentService> logger)
        {
            _graphServiceClient = graphServiceClient;
            _logger = logger;
        }

        /// <summary>
        /// Returns the set of scopes to render on the picker: requested + previously granted + any
        /// sub-resource (SMART v2 granular) alternatives exposed by the resource app.
        /// </summary>
        public async Task<AppConsentInfo> GetAppConsentScopes(string requestingAppClientId, string userId, string[] requestedScopes)
        {
            var requestingClientApp = await GetRequestingApplication(requestingAppClientId);
            var resourceServicePrincipals = await GetResourceServicePrincipals(
                requestingClientApp.RequiredResourceAccess!.Select(x => x.ResourceAppId!).Distinct());
            var requestingServicePrincipal = await GetRequestingServicePrincipal(requestingAppClientId);
            var permissions = await GetUserAppOAuth2PermissionGrants(requestingServicePrincipal.Id!, userId);

            AppConsentInfo info = new()
            {
                ApplicationId = requestingClientApp.AppId,
                ApplicationName = requestingClientApp.DisplayName,
                ApplicationDescription = requestingClientApp.Description,
                ApplicationUrl = requestingClientApp.Info?.MarketingUrl ?? string.Empty,
            };

            var requestedAndApprovedScopes = string.Join(" ", permissions.Select(x => x.Scope))
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Union(requestedScopes, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Exclude Microsoft Graph SP from the "all scopes on this resource" pool.
            const string GraphAppId = "00000003-0000-0000-c000-000000000000";
            var allSpScopes = resourceServicePrincipals.Values
                .Where(sp => sp.AppId != GraphAppId && sp.Oauth2PermissionScopes != null)
                .SelectMany(sp => sp.Oauth2PermissionScopes!)
                .ToList();

            // Expand each requested/approved scope with any granular variants the resource app exposes.
            // "patient/Observation.rs" -> also surface "patient/Observation.rs?category=..." variants.
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

            var requestingClientAppScopeIds = GetAppScopeIds(requestingClientApp);
            foreach (var scope in requestedApprovedAndGranularScopes)
            {
                var matchingResourcePrincipal = resourceServicePrincipals.Values.FirstOrDefault(
                    x => x.Oauth2PermissionScopes!.Any(
                        y => requestingClientAppScopeIds.Contains((Guid)y.Id!) && y.Value == scope));

                var scopeInfo = matchingResourcePrincipal?.Oauth2PermissionScopes!.FirstOrDefault(x => x.Value == scope);
                var scopeConsentRecord = permissions.SingleOrDefault(
                    x => x.ResourceId == matchingResourcePrincipal?.Id &&
                         x.Scope!.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                             .Any(s => s.Equals(scope, StringComparison.OrdinalIgnoreCase)));

                if (matchingResourcePrincipal is not null && scopeInfo?.Id is not null)
                {
                    info.Scopes.Add(new AppConsentScope
                    {
                        Name = scopeInfo.Value,
                        Id = scopeInfo.Id.ToString(),
                        ResourceId = matchingResourcePrincipal.Id,
                        Consented = scopeConsentRecord is not null,
                        ConsentId = scopeConsentRecord?.Id,
                        UserDescription = scopeInfo.UserConsentDescription,
                    });
                }
            }

            return info;
        }

        /// <summary>
        /// Persists the user's picker selection. Only removals are honored here: Entra will re-prompt
        /// (or the picker will PATCH) for anything the user adds. The grant is deleted or updated
        /// per resource.
        /// </summary>
        public async Task PersistAppConsentScopeIfRemoval(AppConsentInfo consentInfo, string userId)
        {
            _ = userId; // parameter retained for parity with the reference API; grant id already identifies the user.

            foreach (var resourceId in consentInfo.Scopes.Select(x => x.ResourceId).Distinct())
            {
                var resourceScopes = consentInfo.Scopes.Where(x => x.ResourceId == resourceId).ToList();
                var scopeString = string.Join(" ", resourceScopes.Where(x => x.Consented).Select(x => x.Name));
                if (resourceScopes.Any(x => x.ConsentId is not null))
                {
                    var consentId = resourceScopes.First(x => x.ConsentId is not null).ConsentId!;
                    await UpdateUserAppOAuth2PermissionGrantIfRemovalNeeded(consentId, scopeString);
                }
            }
        }

        private static string GetScopeRoot(string scope)
        {
            var idx = scope.IndexOf('?');
            return idx < 0 ? scope : scope.Substring(0, idx);
        }

        private async Task<Application> GetRequestingApplication(string applicationId)
        {
            var requestingApp = await _graphServiceClient.Applications.GetAsync(rq =>
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
            var requestingServicePrincipal = await _graphServiceClient.ServicePrincipals.GetAsync(rq =>
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
            var appScopeIds = new List<Guid>();
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

                var servicePrincipal = await _graphServiceClient.ServicePrincipals.GetAsync(rq =>
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
            var permissionPage = await _graphServiceClient.Oauth2PermissionGrants.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"clientId eq '{requestingAppClientId}' and consentType eq 'Principal' and principalId eq '{userId}'";
            });

            return permissionPage!.Value!.ToList();
        }

        private async Task UpdateUserAppOAuth2PermissionGrantIfRemovalNeeded(string grantId, string scope)
        {
            var existingGrant = await _graphServiceClient.Oauth2PermissionGrants[grantId].GetAsync();
            var existingScopes = existingGrant!.Scope!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var newScopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Only handle removals; Entra will re-prompt for additions on the next authorize.
            if (existingScopes.Except(newScopes, StringComparer.OrdinalIgnoreCase).Any())
            {
                _logger.LogInformation("Deleting OAuth2PermissionGrant {GrantId} to narrow scopes to {Scope}", grantId, scope);
                await _graphServiceClient.Oauth2PermissionGrants[grantId].DeleteAsync();
            }
        }
    }
}
