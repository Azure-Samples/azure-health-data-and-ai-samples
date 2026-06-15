// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Strategies
{
   public interface IIdpStrategy
    {
        bool ProvidesAuthorizeProxy { get; }

        /// <summary>
        /// True when the strategy supports SMART v2 backend services (client_credentials + private_key_jwt)
        /// via inbound JWT validation + outbound client_secret swap. Entra only.
        /// </summary>
        bool SupportsBackendServices { get; }

        /// <summary>
        /// JWT claim used as the user identifier on both the User Context (context-cache) token
        /// and the EHR access token. The claim value MUST be identical across both flows for
        /// the launch-context cache to round-trip. Hard-coded per IdP because the correct value
        /// is a property of the IdP, not an operator choice.
        /// </summary>
        string UserIdClaimType { get; }

        Task<string> GetTokenEndpointAsync();
        Task<string> GetAuthorizeEndpointAsync();
        Task<string> GetOpenIdConfigurationUrlAsync();
        string TranslateScopesToIdp(string smartScopes);
        IEnumerable<string> TranslateScopesFromIdp(IEnumerable<string> idpScopes);

        /// <summary>
        /// Builds the outbound scope value sent to the upstream IdP for backend services.
        /// Entra requires "{fhirAudience}/.default".
        /// </summary>
        string BuildBackendScope(string fhirAudience);
    }
}
