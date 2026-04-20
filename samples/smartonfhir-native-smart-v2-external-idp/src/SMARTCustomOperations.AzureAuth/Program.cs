// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Microsoft.AzureHealth.DataServices.Bindings;
using Microsoft.AzureHealth.DataServices.Caching;
using Microsoft.AzureHealth.DataServices.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Filters;
using SMARTCustomOperations.AzureAuth.Services;

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

                    // Discover IDP token endpoint from FHIR's .well-known/smart-configuration
                    if (!string.IsNullOrEmpty(config.IdpTokenEndpoint))
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

                    // Cache for launch context (required for EHR launch, optional for standalone)
                    services.AddMemoryCache();
                    if (!string.IsNullOrEmpty(config.CacheConnectionString))
                    {
                        services.AddRedisCacheBackingStore(options =>
                        {
                            options.ConnectionString = config.CacheConnectionString;
                        });
                        services.AddJsonObjectMemoryCache(options =>
                        {
                            options.CacheItemExpiry = TimeSpan.FromSeconds(3600);
                        });
                        services.AddScoped<ContextCacheService>();
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
