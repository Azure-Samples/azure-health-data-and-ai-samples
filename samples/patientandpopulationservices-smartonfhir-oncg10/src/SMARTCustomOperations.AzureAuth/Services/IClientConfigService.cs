// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public interface IClientConfigService
    {
        Task<BackendClientConfiguration> FetchBackendClientConfiguration(string clientId);

        Task<ServiceBaseObject.Endpoint> FetchEndpointConfiguration(List<string> keys);

        Task<ServiceBaseObject.Organization> FetchOrganizationConfiguration(List<string> keys, string endpointId);
    }
}
