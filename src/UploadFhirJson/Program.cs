using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using System.Reflection;
using UploadFhirJson.Caching;
using UploadFhirJson.Configuration;
using UploadFhirJson.FhirClient;
using UploadFhirJson.ProcessFhir;

ServiceConfiguration config = new();

using IHost host = new HostBuilder()
    .ConfigureAppConfiguration((hostingContext, configuration) =>
    {
        configuration.Sources.Clear();
        configuration
            .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
            .AddEnvironmentVariables("AZURE_");

        IConfigurationRoot configurationRoot = configuration.Build();
        configurationRoot.Bind(config);
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        if (config.AppInsightConnectionstring != null)
        {
            services.AddLogging(builder =>
            {
                builder.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
                builder.AddApplicationInsights(op => op.ConnectionString = config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
            });

            services.Configure<TelemetryConfiguration>(options =>
            {
                options.ConnectionString = config.AppInsightConnectionstring;
            });
            services.AddTransient<TelemetryClient>();
        }

        BlobConfiguration blobConfig = new()
        {
            BlobConnectionString = config.BlobConnectionString,
            ProcessedBlobContainer = config.ProcessedBlobContainer,
            FhirFailedBlob = config.FhirFailedBlob,
            HL7FailedBlob = config.HL7FailedBlob,
            SkippedBlobContainer = config.SkippedBlobContainer,
            ConvertedContainer = config.ConvertedContainer,
            FailedBlobContainer = config.FailedBlobContainer,
            FhirJsonContainer = config.FhirJsonContainer

        };
        services.AddSingleton(blobConfig);

        services.AddMemoryCache();
        services.AddScoped<IProcessFhirJson, ProcessFhirJson>();

        services.AddSingleton<IInMemoryCache, InMemoryCache>();
        services.AddHttpClient<IFhirClient, FhirClient>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(config.FhirURL);
        });

        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(config.BlobConnectionString);
        });


    })
    .Build();

await host.RunAsync();
