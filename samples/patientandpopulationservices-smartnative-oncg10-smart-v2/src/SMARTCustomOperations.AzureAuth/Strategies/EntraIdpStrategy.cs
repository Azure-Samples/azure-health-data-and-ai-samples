// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;

namespace SMARTCustomOperations.AzureAuth.Strategies
{
    public sealed class EntraIdpStrategy : IIdpStrategy
    {
        private readonly AzureAuthOperationsConfig _config;

        public EntraIdpStrategy(AzureAuthOperationsConfig config)
        {
            _config = config;
        }

        public bool ProvidesAuthorizeProxy => true;

        public bool SupportsBackendServices => true;

        public bool ProvidesConsentPicker => true;

        // Entra emits a stable object id in the "oid" claim on both id_tokens and access_tokens.
        public string UserIdClaimType => "oid";

        public Task<string> GetTokenEndpointAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/token");

        public Task<string> GetAuthorizeEndpointAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/authorize");

        public Task<string> GetOpenIdConfigurationUrlAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/v2.0/.well-known/openid-configuration");

        public string TranslateScopesToIdp(string smartScopes) =>
            ScopeFormat.ToEntraFormat(smartScopes, _config.FhirAudience ?? string.Empty);

        public IEnumerable<string> TranslateScopesFromIdp(IEnumerable<string> idpScopes) =>
            ScopeFormat.ToSmartFormat(idpScopes, _config.FhirAudience ?? string.Empty);

        /// <summary>
        /// Entra only accepts "{resource}/.default" for client_credentials (AADSTS1002012).
        /// </summary>
        public string BuildBackendScope(string fhirAudience) =>
            $"{(fhirAudience ?? string.Empty).TrimEnd('/')}/.default";
    }
}
