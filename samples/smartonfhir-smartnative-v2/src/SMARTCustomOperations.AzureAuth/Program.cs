// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Microsoft.AzureHealth.DataServices.Bindings;
using Microsoft.AzureHealth.DataServices.Caching;
using Microsoft.AzureHealth.DataServices.Caching.StorageProviders;
using Microsoft.AzureHealth.DataServices.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Filters;
using SMARTCustomOperations.AzureAuth.Services;
using SMARTCustomOperations.AzureAuth.Strategies;

#pragma warning disable IDE0005

namespace SMARTCustomOperations.AzureAuth
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            AzureAuthOperationsConfig config = new();

            // Discover the IDP token endpoint before building the host.
            // RestBinding needs the correct base address (scheme+host) at startup.
            string discoveredTokenEndpoint = null!;

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.Sources.Clear();
                    IHostEnvironment env = context.HostingEnvironment;

                    configuration
                        .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables("AZURE_");

                    IConfigurationRoot configurationRoot = configuration.Build();
                    configurationRoot.Bind(config);
                    config.Validate();

                    if (string.Equals(config.IdpType, "EntraId", StringComparison.OrdinalIgnoreCase))
                    {
                        // Entra mode: token endpoint is fixed by tenant; no FHIR well-known discovery needed at startup.
                        discoveredTokenEndpoint = $"https://login.microsoftonline.com/{config.TenantId}/oauth2/v2.0/token";
                        Console.WriteLine($"Entra mode. Token endpoint: {discoveredTokenEndpoint}");
                    }
                    else if (!string.IsNullOrEmpty(config.IdpTokenEndpoint))
                    {
                        discoveredTokenEndpoint = config.IdpTokenEndpoint;
                        Console.WriteLine($"Using configured IDP token endpoint: {discoveredTokenEndpoint}");
                    }
                    else
                    {
                        var fhirUrl = config.FhirServerUrl!.TrimEnd('/');
                        var smartConfigUrl = $"{fhirUrl}/.well-known/smart-configuration";
                        Console.WriteLine($"Discovering IDP token endpoint from {smartConfigUrl}");

                        using var httpClient = new HttpClient();
                        var json = httpClient.GetStringAsync(smartConfigUrl).GetAwaiter().GetResult();
                        var doc = System.Text.Json.JsonDocument.Parse(json);
                        discoveredTokenEndpoint = doc.RootElement.GetProperty("token_endpoint").GetString()
                            ?? throw new InvalidOperationException("token_endpoint not found in FHIR SMART configuration.");

                        Console.WriteLine($"Discovered IDP token endpoint: {discoveredTokenEndpoint}");
                    }
                })
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    if (config.AppInsightsConnectionString is not null)
                    {
                        services.UseAppInsightsLogging(config.AppInsightsConnectionString, LogLevel.Information);
                        services.UseTelemetry(config.AppInsightsConnectionString);
                    }

                    services.AddSingleton<AzureAuthOperationsConfig>(config);

                    // HttpClient for FHIR proxy and SMART config discovery
                    services.AddHttpClient("FhirProxy");
                    services.AddHttpClient("FhirSmartConfig");

                    // Service that discovers IDP endpoints from FHIR's .well-known/smart-configuration
                    services.AddSingleton<FhirSmartConfigService>();

                    // IdP strategy: Entra translates SMART scopes and proxies authorize/token to Entra v2;
                    // External is a pass-through to the IdP discovered from FHIR SMART well-known.
                    if (string.Equals(config.IdpType, "EntraId", StringComparison.OrdinalIgnoreCase))
                    {
                        services.AddSingleton<IIdpStrategy, EntraIdpStrategy>();
                    }
                    else
                    {
                        services.AddSingleton<IIdpStrategy, ExternalIdpStrategy>();
                    }

                    // Context cache for EHR launch (patient/encounter context).
                    // The JsonObjectCache requires an ICacheBackingStoreProvider in DI. Default is
                    // a no-op provider so the cache is strictly in-memory (process-scoped) — fine
                    // for dev and single-instance use. Set AZURE_CacheConnectionString to a
                    // Redis-compatible connection string (e.g. Azure Managed Redis) to add a
                    // distributed backing store that survives restarts and works across instances.
                    //
                    // MemoryCache is also used by MemoryAssertionReplayProtector for backend-services
                    // jti replay tracking. Cap size so a flood of unique jti values cannot grow memory
                    // unbounded within the 5-minute TTL window. Entries opt in via Size=1 in the
                    // protector; consumers that omit Size are not counted.
                    services.AddMemoryCache(options =>
                    {
                        options.SizeLimit = 10_000;
                        options.CompactionPercentage = 0.2;
                    });
                    services.AddJsonObjectMemoryCache(options =>
                    {
                        options.CacheItemExpiry = TimeSpan.FromSeconds(3600);
                    });
                    services.AddScoped<ContextCacheService>();
                    if (!string.IsNullOrEmpty(config.CacheConnectionString))
                    {
                        services.AddRedisCacheBackingStore(options =>
                        {
                            options.ConnectionString = config.CacheConnectionString;
                        });
                        Console.WriteLine("Context cache: in-memory with Redis backing store.");
                    }
                    else
                    {
                        services.AddSingleton<ICacheBackingStoreProvider, InMemoryOnlyCacheBackingStoreProvider>();
                        Console.WriteLine("Context cache: in-memory only (set AZURE_CacheConnectionString to add Redis backing).");
                    }

                    // Backend services (SMART v2 client_credentials + private_key_jwt).
                    // Entra cannot validate arbitrary client-registered JWKS, so the proxy validates
                    // the inbound client_assertion and swaps to a KV-stored Entra client_secret.
                    // Enabled only when Entra is the upstream IdP AND a KV store is configured.
                    // External IdPs (Okta, Ping, etc.) handle backend services natively at their own
                    // token endpoint — the proxy is not in the path.
                    bool isEntraId = string.Equals(config.IdpType, "EntraId", StringComparison.OrdinalIgnoreCase);
                    bool hasKvStore = !string.IsNullOrWhiteSpace(config.BackendServiceKeyVaultStore);
                    if (isEntraId && hasKvStore)
                    {
                        services.AddSingleton<IAssertionReplayProtector, MemoryAssertionReplayProtector>();
                        services.AddSingleton<IClientConfigService, KeyVaultClientConfigurationService>();
                        services.AddSingleton<IBackendClientAssertionValidator, BackendClientAssertionValidator>();
                        services.AddHttpClient(); // for JWKS fetch
                        Console.WriteLine($"Backend services proxy enabled. KV store: {config.BackendServiceKeyVaultStore}");
                    }
                    else if (!isEntraId)
                    {
                        Console.WriteLine($"Backend services proxy not applicable for IdpType '{config.IdpType}'. Clients call the IdP token endpoint directly.");
                    }
                    else
                    {
                        Console.WriteLine("Backend services proxy disabled. Set AZURE_BackendServiceKeyVaultStore to enable.");
                    }

                    services.UseAzureFunctionPipeline();

                    services.AddInputFilter(typeof(TokenInputFilter));
                    services.AddInputFilter(typeof(ContextCacheInputFilter));

                    // RestBinding base address: scheme+host from the discovered IDP token endpoint.
                    // TokenInputFilter sets the full path at runtime.
                    var tokenUri = new Uri(discoveredTokenEndpoint);
                    var restBaseAddress = $"{tokenUri.Scheme}://{tokenUri.Authority}/";
                    services.AddBinding<RestBinding, RestBindingOptions>(options =>
                    {
                        options.BaseAddress = new Uri(restBaseAddress);
                    });

                    services.AddOutputFilter(typeof(TokenOutputFilter));
                })
                .Build();

            await host.RunAsync();
        }
    }
}
