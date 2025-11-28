// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using Azure.Identity;
using Microsoft.AzureHealth.DataServices.Bindings;
using Microsoft.AzureHealth.DataServices.Configuration;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.Export.Configuration;
using SMARTCustomOperations.Export.Filters;
using SMARTCustomOperations.Export.Services;

namespace SMARTCustomOperations.Export
{
    internal class Program
    {
        internal static async Task Main(string[] args)
        {
            ExportCustomOperationsConfig config = new();
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

                    // config.Validate();
                })
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(services =>
                {
                    if (config.AppInsightsConnectionString is not null)
                    {
                        services.UseAppInsightsLogging(config.AppInsightsConnectionString, LogLevel.Trace);
                        services.UseTelemetry(config.AppInsightsConnectionString);
                    }

                    services.UseAzureFunctionPipeline();

                    services.AddSingleton<ExportCustomOperationsConfig>(config);

                    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() { TenantId = config.TenantId } );

                    // Add blob service client to fetch export files for the user
                    services.AddAzureClients(clientBuilder =>
                    {
                        clientBuilder.AddBlobServiceClient(new Uri(config.ExportStorageAccountUrl!))
                            .WithCredential(credential);
                    });

                    services.AddScoped<IExportFileService, ExportFileService>();

                    // First filter sets up the pipeline by extracting properties
                    services.AddInputFilter(typeof(ExtractPipelinePropertiesInputFilter));

                    // removes /api from request
                    services.AddInputFilter(typeof(ReMapRequestUrlInputFilter));

                    // fetches ndjson file for user
                    services.AddInputFilter(typeof(GetExportFileInputFilter));

                    // In the middle is our custom binding that will either hit the FHIR Service or Azure Storage
                    // Since we are using a custom binding, logic can be moved here instead of input filters.

                    services.AddBinding<RestBinding, RestBindingOptions>(options =>
                    {
                        options.BaseAddress = new Uri(config.FhirUrl!);
                        options.Credential = credential;
                        options.PassThroughAuthorizationHeader = true;
                    });

                    // Next is the export operation check output filter to point export URLs to our APIM front end
                    services.AddOutputFilter(typeof(CheckExportJobOutputFilter));

                    // Finally the export operation output filter to change the content-location header
                    services.AddOutputFilter(typeof(ExportOperationOutputFilter));
                })
                .Build();

            await host.RunAsync();
        }
    }
}
