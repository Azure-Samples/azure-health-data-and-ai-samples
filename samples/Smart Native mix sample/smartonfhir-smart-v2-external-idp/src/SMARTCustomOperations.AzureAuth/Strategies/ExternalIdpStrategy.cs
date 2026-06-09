// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth.Strategies
{
    public sealed class ExternalIdpStrategy : IIdpStrategy
    {
        private readonly FhirSmartConfigService _smartConfigService;

        public ExternalIdpStrategy(FhirSmartConfigService smartConfigService)
        {
            _smartConfigService = smartConfigService;
        }

        public bool ProvidesAuthorizeProxy => false;

        public bool SupportsBackendServices => false;

        public Task<string> GetTokenEndpointAsync() => _smartConfigService.GetTokenEndpointAsync();

        public async Task<string> GetAuthorizeEndpointAsync()
        {
            var config = await _smartConfigService.GetSmartConfigurationAsync();
            return config.TryGetValue("authorization_endpoint", out var v) && v.GetString() is { } s
                ? s
                : throw new InvalidOperationException("authorization_endpoint not found in FHIR SMART configuration.");
        }

        public async Task<string> GetOpenIdConfigurationUrlAsync()
        {
            var authorityUrl = (await _smartConfigService.GetAuthorityUrlAsync()).TrimEnd('/');
            return $"{authorityUrl}/.well-known/openid-configuration";
        }

        public string TranslateScopesToIdp(string smartScopes) => smartScopes;

        public IEnumerable<string> TranslateScopesFromIdp(IEnumerable<string> idpScopes) => idpScopes;

        public string BuildBackendScope(string fhirAudience) =>
            throw new NotSupportedException("Backend services flow is not implemented for the External IdP path.");
    }
}
