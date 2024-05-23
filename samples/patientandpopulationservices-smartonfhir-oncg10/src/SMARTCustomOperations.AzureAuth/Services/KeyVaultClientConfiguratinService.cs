// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Configuration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class KeyVaultClientConfiguratinService : IClientConfigService
    {
        private readonly string _vaultName;
        private readonly string _vaultUrl;
        private readonly ILogger _logger;

        public KeyVaultClientConfiguratinService(AzureAuthOperationsConfig config, ILogger<AsymmetricAuthorizationService> logger)
        {
            if (string.IsNullOrEmpty(config.BackendServiceKeyVaultStore))
            {
                throw new ConfigurationErrorsException("BackendServiceKeyVaultStore must be set to use the KeyVaultClientConfiguratinService.");
            }

            _vaultName = config.BackendServiceKeyVaultStore!;
            _vaultUrl = "https://" + config.BackendServiceKeyVaultStore! + ".vault.azure.net";
            _logger = logger;
        }

        public async Task<BackendClientConfiguration> FetchBackendClientConfiguration(string clientId)
        {
            _logger.LogInformation("Attempting to pull backend service client information for {ClientId}", clientId);

            try
            {
                // Create a keyvault client and try to fetch the client info that corresponds to the request
                var client = new SecretClient(new Uri(_vaultUrl), new DefaultAzureCredential());

                var secret = await client.GetSecretAsync(clientId);

                // Create a backend client config from the vault
                var data = new BackendClientConfiguration(clientId, secret.Value.Value, secret.Value.Properties.Tags["jwks_url"]);

                return data;
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.Status == 401)
                {
                    _logger.LogCritical("Application is not setup correctly. Please provide application access to KeyVault via Managed Identity.");
                    throw new ConfigurationErrorsException($"The function app is not correctly configured to access KeyVault.", ex);
                }

                if (ex.Status == 404)
                {
                    throw new UnauthorizedAccessException($"KeyVault could not find the secret with the name {clientId}", ex);
                }

                _logger.LogError("Unexpected error encountered while accessing KeyVault for client {ClientId}", ex);
                throw new UnauthorizedAccessException($"KeyVault error finding the client with the name {clientId}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error encountered while accessing KeyVault for client {ClientId}", ex);
                throw new UnauthorizedAccessException($"KeyVault error when trying to {clientId}", ex);
            }
        }

        public async Task<ServiceBaseObject.Endpoint> FetchEndpointConfiguration(List<string> keys)
        {
            string endpointKeys = string.Join(", ", keys);
            _logger.LogInformation($"Attempting to pull Endpoint information for {endpointKeys}");
            try
            {
                // Create a keyvault client and try to fetch the client info that corresponds to the request
                var client = new SecretClient(new Uri(_vaultUrl), new DefaultAzureCredential());

                var status = await client.GetSecretAsync(keys[0]);
                var connectionType = await client.GetSecretAsync(keys[1]);
                var address = await client.GetSecretAsync(keys[2]);

                // Create a Endpoint from the vault
                var data = new ServiceBaseObject.Endpoint
                {
                    Status = status.Value.Value,
                    ConnectionType = new ServiceBaseObject.Endpoint.ConnectionTypeClass
                    {
                        System = connectionType.Value.Value,
                        Code = "hl7-fhir-rest"
                    },
                    PayloadType = new List<ServiceBaseObject.Endpoint.PayloadTypeClass>
                    {
                        new ServiceBaseObject.Endpoint.PayloadTypeClass()
                        {
                            Coding = new List<ServiceBaseObject.Endpoint.PayloadTypeClass.CodingClass>
                            {
                                new ServiceBaseObject.Endpoint.PayloadTypeClass.CodingClass()
                            }
                        }
                    },
                    Address = address.Value.Value
                };

                return data;
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.Status == 401)
                {
                    _logger.LogCritical("Application is not setup correctly. Please provide application access to KeyVault via Managed Identity.");
                    throw new ConfigurationErrorsException($"The function app is not correctly configured to access KeyVault.", ex);
                }

                if (ex.Status == 404)
                {
                    throw new UnauthorizedAccessException($"KeyVault could not find the secret with the names {endpointKeys}", ex);
                }

                _logger.LogError("Unexpected error encountered while accessing KeyVault for keys {EndpointKeys}", ex);
                throw new UnauthorizedAccessException($"KeyVault error finding the keys with the names {endpointKeys}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error encountered while accessing KeyVault for keys {EndpointKeys}", ex);
                throw new UnauthorizedAccessException($"KeyVault error when trying to {endpointKeys}", ex);
            }
        }

        public async Task<ServiceBaseObject.Organization> FetchOrganizationConfiguration(List<string> keys, string endpointId)
        {
            string orgKeys = string.Join(", ", keys);
            _logger.LogInformation($"Attempting to pull Endpoint information for {orgKeys}");
            try
            {
                // Create a keyvault client and try to fetch the client info that corresponds to the request
                var client = new SecretClient(new Uri(_vaultUrl), new DefaultAzureCredential());

                var active = await client.GetSecretAsync(keys[0]);
                var name = await client.GetSecretAsync(keys[1]);
                var location = await client.GetSecretAsync(keys[2]);
                var identifier = await client.GetSecretAsync(keys[3]);

                // Create a Endpoint from the vault
                var data = new ServiceBaseObject.Organization
                {
                    Identifier = new List<ServiceBaseObject.Organization.IdentifierClass>
                    {
                        new ServiceBaseObject.Organization.IdentifierClass
                        {
                            System = identifier.Value.Value,
                            Value = "1407071210"
                        }
                    },
                    Active = bool.Parse(active.Value.Value),
                    Name = name.Value.Value,
                    Address = new List<ServiceBaseObject.Organization.AddressClass>
                    {
                        new ServiceBaseObject.Organization.AddressClass
                        {
                            Line = new List<string> { "3300 Washtenaw Avenue, Suite 227" },
                            City = "Ann Arbor",
                            State = "MI",
                            PostalCode = "48104",
                            Country = location.Value.Value
                        }
                    },
                    Endpoint = new List<ServiceBaseObject.Organization.EndpointReference>
                    {
                        new ServiceBaseObject.Organization.EndpointReference
                        {
                            Reference = $"Endpoint/{endpointId}"
                        }
                    }
                };

                return data;
            }
            catch (Azure.RequestFailedException ex)
            {
                if (ex.Status == 401)
                {
                    _logger.LogCritical("Application is not setup correctly. Please provide application access to KeyVault via Managed Identity.");
                    throw new ConfigurationErrorsException($"The function app is not correctly configured to access KeyVault.", ex);
                }

                if (ex.Status == 404)
                {
                    throw new UnauthorizedAccessException($"KeyVault could not find the secret with the names {orgKeys}", ex);
                }

                _logger.LogError("Unexpected error encountered while accessing KeyVault for keys {OrgKeys}", ex);
                throw new UnauthorizedAccessException($"KeyVault error finding the keys with the names {orgKeys}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error encountered while accessing KeyVault for keys {OrgKeys}", ex);
                throw new UnauthorizedAccessException($"KeyVault error when trying to {orgKeys}", ex);
            }
        }
    }
}
