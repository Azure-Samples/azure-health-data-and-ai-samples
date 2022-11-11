using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using System.Reflection;
using UploadFhirJson.Model;
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
            SuccessBlobContainer = config.SuccessBlobContainer,
            FhirFailedBlob = config.FhirFailedBlob,
            HL7FailedBlob = config.HL7FailedBlob,
            SkippedBlobContainer = config.SkippedBlobContainer,
            ValidatedBlobContainer = config.ValidatedBlobContainer,
            FailedBlobContainer = config.FailedBlobContainer
        };
        services.AddSingleton(blobConfig);

        services.AddTransient<IProcessFhirJson, ProcessFhirJson>();
        services.AddHttpClient<IFhirService, FhirService>(httpClient =>
        {
            httpClient.BaseAddress = new Uri(config.FhirURL);

        });
    })
    .Build();

await host.RunAsync();
