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
        private string? _apiManagementHostName;

        // Example: my-apim.azure-api.net
        public string? ApiManagementHostName
        {
            get
            {
                if (_apiManagementHostName is null)
                {
                    return null;
                }

                return _apiManagementHostName.Replace("https://", string.Empty, StringComparison.InvariantCultureIgnoreCase);
            }

            set
            {
                _apiManagementHostName = value;
            }
        }

        public string? ApiManagementFhirPrefex { get; set; } = "smart";

        // Returns more detailed error messages to client
        public bool Debug { get; set; } = false;

        public string? AppInsightsConnectionString { get; set; }

        public string? AppInsightsInstrumentationKey { get; set; }

        public string? TenantId { get; set; }

        public bool SmartonFhir_with_B2C { get; set; }

        public string? Authority_URL { get; set; }

        public string? B2C_Tenant_Id { get; set; }

        public string? Fhir_Resource_AppId { get; set; }

        public string? BackendServiceKeyVaultStore { get; set; }

        public string? FhirAudience
        {
            get => _fhirAudience;
            set
            {
                if (value is not null && value.Length > 0)
                {
                    if (!value.EndsWith("/", StringComparison.InvariantCultureIgnoreCase))
                    {
                        _fhirAudience = value + "/";
                    }
                    else
                    {
                        _fhirAudience = value;
                    }
                }
            }
        }

        public string? ContextAppClientId { get; set; }

        // Only for static environment config service - not for production.
        public string? TestBackendClientId { get; set; }

        // Only for static environment config service - not for production.
        public string? TestBackendClientSecret { get; set; }

        // Only for static environment config service - not for production.
        public string? TestBackendClientJwks { get; set; }

        public string? CacheConnectionString { get; set; }

        public string? CacheContainer { get; set; }

        public string? Issuer { get; set; }

        public string? Authorization_Endpoint { get; set; }

        public string? Token_Endpoint { get; set; }

        public string KeyVaultClientIdKey { get; set; } = "ExternalAppClientID";

        public string KeyVaultClientSecretKey { get; set; } = "ExternalAppClientSecret";

        // Only for testing 
        public bool? IsCachedData { get; set; } = false;

        public void Validate()
        {
            if (string.IsNullOrEmpty(ApiManagementHostName))
            {
                throw new ConfigurationErrorsException("ApiManagementHostName must be configured for this application.");
            }

            if (string.IsNullOrEmpty(TenantId))
            {
                throw new ConfigurationErrorsException("TenantId must be configured for this application.");
            }

            if (string.IsNullOrEmpty(FhirAudience))
            {
                throw new ConfigurationErrorsException("Audience must be configured for this application.");
            }

            if (string.IsNullOrEmpty(ContextAppClientId))
            {
                throw new ConfigurationErrorsException("ContextAppClientId must be configured for this application.");
            }

            if (string.IsNullOrEmpty(CacheConnectionString))
            {
                throw new ConfigurationErrorsException("CacheConnectionString must be configured for this application.");
            }
        }
    }
}
