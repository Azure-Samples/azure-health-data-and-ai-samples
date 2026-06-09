// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Backend service client registration material:
    /// the Entra app's client_secret used to call Entra <c>/token</c> with client_credentials,
    /// plus the JWKS URI used to verify the inbound client_assertion.
    /// </summary>
    public class BackendClientConfiguration
    {
        public BackendClientConfiguration(string clientId, string clientSecret, Uri jwksUri)
        {
            ClientId = clientId;
            ClientSecret = clientSecret;
            JwksUri = jwksUri;
        }

        public string ClientId { get; }

        public string ClientSecret { get; }

        public Uri JwksUri { get; }
    }
}
