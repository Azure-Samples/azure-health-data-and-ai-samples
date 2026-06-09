// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Configuration;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public class AzureAuthOperationsConfig
    {
        private string? _fhirAudience;

        public bool Debug { get; set; } = false;

        public string? AppInsightsConnectionString { get; set; }

        /// <summary>
        /// Selects the upstream IdP integration mode.
        /// "EntraId" — gateway proxies authorize/token to Microsoft Entra and translates SMART scopes.
        /// "External" — gateway forwards to an external IdP (Okta, Ping, etc.) that emits SMART scopes natively.
        /// </summary>
        public string IdpType { get; set; } = "EntraId";

        /// <summary>
        /// Microsoft Entra tenant id. Required when <see cref="IdpType"/> is "EntraId".
        /// </summary>
        public string? TenantId { get; set; }

        public string? FhirServerUrl { get; set; }

        /// <summary>
        /// Legacy setting retained for backward compatibility.
        /// Token forwarding now always uses token_endpoint from FHIR SMART well-known discovery.
        /// </summary>
        public string? IdpTokenEndpoint { get; set; }

        /// <summary>
        /// Optional override: The IDP's OpenID Connect authority URL (e.g., https://my-okta-domain.okta.com/oauth2/default).
        /// If not set, auto-discovered from the FHIR Service's .well-known/smart-configuration issuer.
        /// </summary>
        public string? Authority_URL { get; set; }

        public string? FhirAudience
        {
            get => _fhirAudience;
            set
            {
                if (value is not null && value.Length > 0)
                {
                    _fhirAudience = value.EndsWith("/", StringComparison.InvariantCultureIgnoreCase)
                        ? value
                        : value + "/";
                }
            }
        }

        /// <summary>
        /// The claim type in the IDP's token that identifies the user (e.g., "sub", "uid", "oid").
        /// </summary>
        public string UserIdClaimType { get; set; } = "sub";

        public string? CacheConnectionString { get; set; }

        public string? ContextAppClientId { get; set; }

        /// <summary>
        /// Key Vault name (or full https vault URI) hosting backend client registrations.
        /// Each registered client = one secret: name=clientId, value=Entra client_secret,
        /// tag "jwks_url"=HTTPS URL of client's JWKS. Leave empty to disable backend services.
        /// </summary>
        public string? BackendServiceKeyVaultStore { get; set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(FhirServerUrl))
            {
                throw new ConfigurationErrorsException("FhirServerUrl must be configured for this application.");
            }

            if (string.IsNullOrEmpty(FhirAudience))
            {
                throw new ConfigurationErrorsException("FhirAudience must be configured for this application.");
            }

            if (string.IsNullOrEmpty(CacheConnectionString))
            {
                System.Diagnostics.Trace.TraceWarning("CacheConnectionString is not configured. EHR launch context caching will be unavailable.");
            }

            if (string.Equals(IdpType, "EntraId", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(TenantId))
                {
                    throw new ConfigurationErrorsException("TenantId must be configured when IdpType is 'EntraId'.");
                }
            }
            else
            {
                System.Diagnostics.Trace.TraceInformation("IdpTokenEndpoint setting is ignored. token_endpoint is always read from FHIR SMART well-known metadata.");

                if (string.IsNullOrEmpty(Authority_URL))
                {
                    System.Diagnostics.Trace.TraceInformation("Authority_URL is not configured. Will be auto-discovered from FHIR Service's .well-known/smart-configuration.");
                }
            }
        }
    }
}
