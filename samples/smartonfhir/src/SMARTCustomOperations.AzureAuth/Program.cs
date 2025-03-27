// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Azure.Identity;
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


namespace SMARTCustomOperations.AzureAuth
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            AzureAuthOperationsConfig config = new();
            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.Sources.Clear();
                    IHostEnvironment env = context.HostingEnvironment;

                    // Pull configuration from user secrets and local settings for local dev
                    // Pull from environment variables for Azure deployment
                    configuration
                        .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                        .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables("AZURE_");

                    IConfigurationRoot configurationRoot = configuration.Build();
                    configurationRoot.Bind(config);
                    config.Validate();
                })
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    if (config.AppInsightsConnectionString is not null)
                    {
                        services.UseAppInsightsLogging(config.AppInsightsConnectionString, LogLevel.Information);
                        services.UseTelemetry(config.AppInsightsConnectionString);
                    }                   
                    // Add configuration
                    services.AddSingleton<AzureAuthOperationsConfig>(config);

                    // Add services needed for Microsoft Graph
                    services.AddMicrosoftGraphClient(options =>
                    {
                        options.Credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { TenantId = config.TenantId });
                    });
                    services.AddScoped<GraphConsentService>();

                    // Add cache for token context
                    services.AddMemoryCache();
                    services.AddRedisCacheBackingStore(options =>
                    {
                        options.ConnectionString = config.CacheConnectionString;
                    });
                    services.AddJsonObjectMemoryCache(options =>
                    {
                        options.CacheItemExpiry = TimeSpan.FromSeconds(3600);
                    });
                    services.AddScoped<ContextCacheService>();

                    // Use the toolkit Azure Function pipeline
                    services.UseAzureFunctionPipeline();

                    // Add toolkit elements
                    services.AddInputFilter(typeof(AuthorizeInputFilter));
                    services.AddInputFilter(typeof(TokenInputFilter));
                    services.AddInputFilter(typeof(AppConsentInfoInputFilter));
                    services.AddInputFilter(typeof(ContextCacheInputFilter));

                    services.AddBinding<RestBinding, RestBindingOptions>(options =>
                    {
                        options.BaseAddress = new Uri("https://login.microsoftonline.com");
                    });

                    services.AddOutputFilter(typeof(TokenOutputFilter));
                })
                .Build();

            await host.RunAsync();
        }
    }
}
