using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using SMARTOnFhir.Server.Configuration;

namespace SMARTOnFhir.Server.Services
{
    /// <summary>
    /// Centralizes Microsoft Graph operations that touch OAuth2 permission grants
    /// (per-user delegated-permission consent records) for SMART clients against
    /// the FHIR resource service principal.
    /// </summary>
    public class ConsentService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly SmartConfig _config;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ConsentService> _logger;

        public ConsentService(
            GraphServiceClient graphClient,
            IOptions<SmartConfig> config,
            IMemoryCache cache,
            ILogger<ConsentService> logger)
        {
            _graphClient = graphClient;
            _config = config.Value;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// Deletes the user's OAuth2PermissionGrant for (clientApp → FHIR resource) after a
        /// successful token issuance. The next /authorize for the same (user, client) pair will
        /// not find a stored grant and Entra will prompt the user to consent again with only the
        /// scopes requested in that request. This guarantees that no previously-consented scope
        /// can carry over into a future token, matching the SMART-on-FHIR "ask each time" posture
        /// without requiring the React consent UI.
        ///
        /// If no grant exists yet, nothing happens.
        /// If the grant was admin-consented (consentType=AllPrincipals), it is left untouched
        /// (we only filter for consentType=Principal).
        /// All errors are swallowed and logged — consent reset must never fail token issuance.
        /// </summary>
        public async Task NormalizeUserConsentAsync(string clientId, string userOid, IEnumerable<string> currentScopes)
        {
            try
            {
                var requestingSpId = await GetServicePrincipalIdAsync(clientId);
                var fhirSpId = await GetServicePrincipalIdAsync(_config.FhirResourceAppId);

                if (string.IsNullOrEmpty(requestingSpId) || string.IsNullOrEmpty(fhirSpId))
                {
                    _logger.LogWarning("ConsentService: cannot resolve SP for client {ClientId} or FHIR {FhirAppId}",
                        clientId, _config.FhirResourceAppId);
                    return;
                }

                // Microsoft Graph only allows filtering oauth2PermissionGrants on up to 3 fields
                // (clientId, consentType, principalId). resourceId must be filtered client-side.
                var grants = await _graphClient.Oauth2PermissionGrants.GetAsync(rq =>
                {
                    rq.QueryParameters.Filter =
                        $"clientId eq '{requestingSpId}' and consentType eq 'Principal' " +
                        $"and principalId eq '{userOid}'";
                });

                var grant = grants?.Value?.FirstOrDefault(g => g.ResourceId == fhirSpId);
                if (grant == null)
                {
                    _logger.LogInformation(
                        "ConsentService: no per-user grant found for user {UserId} on client {ClientId} → nothing to delete",
                        userOid, clientId);
                    return;
                }

                _logger.LogInformation(
                    "ConsentService: deleting grant {GrantId} for user {UserId} (scope was [{Scope}]). " +
                    "User will be re-prompted on next /authorize.",
                    grant.Id, userOid, grant.Scope);

                await _graphClient.Oauth2PermissionGrants[grant.Id].DeleteAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ConsentService: NormalizeUserConsentAsync failed (non-fatal) for user {UserId}, client {ClientId}",
                    userOid, clientId);
            }
        }

        /// <summary>
        /// Resolves an app's service principal ID by appId, with in-memory caching.
        /// SP IDs do not change for the lifetime of an app registration.
        /// </summary>
        private async Task<string?> GetServicePrincipalIdAsync(string appId)
        {
            if (string.IsNullOrEmpty(appId))
            {
                return null;
            }

            var cacheKey = $"spId:{appId}";
            if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            var sps = await _graphClient.ServicePrincipals.GetAsync(rq =>
            {
                rq.QueryParameters.Filter = $"appId eq '{appId}'";
            });

            var spId = sps?.Value?.FirstOrDefault()?.Id;
            if (!string.IsNullOrEmpty(spId))
            {
                _cache.Set(cacheKey, spId, TimeSpan.FromHours(12));
            }
            return spId;
        }
    }
}
