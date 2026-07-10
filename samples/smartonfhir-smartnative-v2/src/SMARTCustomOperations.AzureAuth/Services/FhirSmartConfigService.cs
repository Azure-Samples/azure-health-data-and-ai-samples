// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Discovers and caches the SMART on FHIR configuration from the real FHIR Service's
    /// /.well-known/smart-configuration endpoint. All IDP URLs (authorize, token, issuer, etc.)
    /// are derived from this single source — no need to configure them separately.
    /// </summary>
    public class FhirSmartConfigService
    {
        private readonly ILogger<FhirSmartConfigService> _logger;
        private readonly HttpClient _httpClient;
        private readonly AzureAuthOperationsConfig _config;
        private Dictionary<string, JsonElement>? _cachedConfig;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FhirSmartConfigService(ILogger<FhirSmartConfigService> logger, AzureAuthOperationsConfig config, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _config = config;
            _httpClient = httpClientFactory.CreateClient("FhirSmartConfig");
        }

        /// <summary>
        /// Returns the full SMART configuration from the FHIR Service, fetched and cached on first call.
        /// </summary>
        public async Task<Dictionary<string, JsonElement>> GetSmartConfigurationAsync()
        {
            if (_cachedConfig is not null)
            {
                return _cachedConfig;
            }

            await _lock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_cachedConfig is not null)
                {
                    return _cachedConfig;
                }

                var fhirBaseUrl = _config.FhirServerUrl?.TrimEnd('/');
                if (string.IsNullOrEmpty(fhirBaseUrl))
                {
                    throw new InvalidOperationException("FhirServerUrl must be configured.");
                }

                var url = $"{fhirBaseUrl}/.well-known/smart-configuration";
                _logger.LogInformation("Fetching SMART configuration from {Url}", url);

                var response = await _httpClient.GetStringAsync(url);
                _cachedConfig = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response)
                    ?? throw new InvalidOperationException("FHIR SMART configuration returned empty response.");

                _logger.LogInformation("SMART configuration cached successfully.");
                return _cachedConfig;
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Gets the IDP token endpoint from the FHIR SMART configuration.
        /// This enforces discovery-driven routing so proxy forwarding always matches well-known metadata.
        /// </summary>
        public async Task<string> GetTokenEndpointAsync()
        {
            var config = await GetSmartConfigurationAsync();
            return config.TryGetValue("token_endpoint", out var value)
                ? value.GetString() ?? throw new InvalidOperationException("token_endpoint is null in SMART configuration.")
                : throw new InvalidOperationException("token_endpoint not found in SMART configuration.");
        }

        /// <summary>
        /// Gets the IDP's authority/issuer URL from the FHIR SMART configuration.
        /// Falls back to config value if set (for override scenarios).
        /// </summary>
        public async Task<string> GetAuthorityUrlAsync()
        {
            if (!string.IsNullOrEmpty(_config.Authority_URL))
            {
                return _config.Authority_URL;
            }

            var config = await GetSmartConfigurationAsync();
            return config.TryGetValue("issuer", out var value)
                ? value.GetString() ?? throw new InvalidOperationException("issuer is null in SMART configuration.")
                : throw new InvalidOperationException("issuer not found in SMART configuration.");
        }
    }
}
