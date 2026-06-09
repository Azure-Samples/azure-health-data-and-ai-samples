// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Resolves backend client configuration from Azure Key Vault.
    /// Convention per registered client:
    ///   - Secret name  = inbound client_id (Entra app id)
    ///   - Secret value = Entra app client_secret
    ///   - Tag "jwks_url" = HTTPS URL of the client's JWKS (used to verify the client_assertion)
    /// </summary>
    public sealed class KeyVaultClientConfigurationService : IClientConfigService
    {
        private const string JwksUrlTagKey = "jwks_url";

        private readonly SecretClient _secretClient;
        private readonly ILogger<KeyVaultClientConfigurationService> _logger;

        public KeyVaultClientConfigurationService(
            AzureAuthOperationsConfig config,
            ILogger<KeyVaultClientConfigurationService> logger)
        {
            if (string.IsNullOrWhiteSpace(config.BackendServiceKeyVaultStore))
            {
                throw new InvalidOperationException(
                    "KeyVaultClientConfigurationService requires BackendServiceKeyVaultStore to be configured.");
            }

            var vaultName = config.BackendServiceKeyVaultStore.Trim();
            var vaultUri = vaultName.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? new Uri(vaultName)
                : new Uri($"https://{vaultName}.vault.azure.net/");

            _secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
            _logger = logger;
        }

        public async Task<BackendClientConfiguration> FetchBackendClientConfiguration(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new UnauthorizedAccessException("Unknown backend client_id.");
            }

            KeyVaultSecret secret;
            try
            {
                Response<KeyVaultSecret> response = await _secretClient.GetSecretAsync(clientId);
                secret = response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Backend client_id {ClientId} not found in Key Vault.", clientId);
                throw new UnauthorizedAccessException("Unknown backend client_id.");
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Key Vault lookup for backend client {ClientId} failed.", clientId);
                throw new InvalidOperationException("Backend client configuration lookup failed.", ex);
            }

            var clientSecret = secret.Value;
            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException(
                    $"Key Vault secret '{clientId}' has no value (expected Entra client_secret).");
            }

            if (secret.Properties.Tags is null ||
                !secret.Properties.Tags.TryGetValue(JwksUrlTagKey, out var jwksUrl) ||
                string.IsNullOrWhiteSpace(jwksUrl))
            {
                throw new InvalidOperationException(
                    $"Key Vault secret '{clientId}' is missing required tag '{JwksUrlTagKey}'.");
            }

            if (!Uri.TryCreate(jwksUrl, UriKind.Absolute, out var jwksUri))
            {
                throw new InvalidOperationException(
                    $"Key Vault tag '{JwksUrlTagKey}' on secret '{clientId}' is not a valid absolute URI: {jwksUrl}");
            }

            return new BackendClientConfiguration(clientId, clientSecret, jwksUri);
        }
    }
}
