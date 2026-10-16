// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Looks up backend service client registration data (Entra client_secret + JWKS URI)
    /// keyed by the client_id presented in the inbound JWT client_assertion.
    /// </summary>
    public interface IClientConfigService
    {
        Task<BackendClientConfiguration> FetchBackendClientConfiguration(string clientId);
    }
}
