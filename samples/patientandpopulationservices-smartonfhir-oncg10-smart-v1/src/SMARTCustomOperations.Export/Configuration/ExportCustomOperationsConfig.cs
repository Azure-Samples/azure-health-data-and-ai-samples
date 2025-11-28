// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.AzureHealth.DataServices.Security;
using System.Configuration;

namespace SMARTCustomOperations.Export.Configuration
{
    public class ExportCustomOperationsConfig
    {
        private string? _apiManagementHostName;

        public string? AppInsightsConnectionString { get; set; }

        // Returns more detailed error messages to client
        public bool Debug { get; set; } = false;

#pragma warning disable CA1056 // Needs to be string to parse from config easily

        // Example: https://<workspace>-<fhir>.fhir.azurehealthcareapis.com
        public string? FhirUrl { get; set; }

        // Example: https://<account>.blob.core.windows.net
        public string? ExportStorageAccountUrl { get; set; }

        public string? TenantId { get; set; }

#pragma warning restore CA1056 // Needs to be string to parse from config easily

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

        public string ApiManagementFhirPrefex { get; set; } = "smart";

        public void Validate()
        {
            if (string.IsNullOrEmpty(FhirUrl))
            {
                throw new ConfigurationErrorsException("FhirUrl must be configured for this application.");
            }

            if (string.IsNullOrEmpty(ExportStorageAccountUrl))
            {
                throw new ConfigurationErrorsException("ExportStorageAccountUrl must be configured for this application.");
            }

            if (string.IsNullOrEmpty(ApiManagementHostName))
            {
                throw new ConfigurationErrorsException("ApiManagementHostName must be configured for this application.");
            }

            if (string.IsNullOrEmpty(TenantId))
            {
                throw new ConfigurationErrorsException("TenantId must be configured for this application.");
            }
        }
    }
}
