// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Security.KeyVault.Secrets;
using Azure;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public interface IClientConfigService
    {
        Task<BackendClientConfiguration> FetchBackendClientConfiguration(string clientId);
		Task<Response<KeyVaultSecret>> FetchSecretAsync(string key);
    }
}
