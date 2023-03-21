using HL7Sequencing.Configuration;
using HL7Sequencing.Sequencing;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using NHapi.Base.Parser;
using System.Reflection;

HL7SequencingConfiguration config = new();

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
            services.AddScoped<TelemetryClient>();
        }
        BlobConfig blobConfig = new()
        {
            BlobConnectionString = config.BlobConnectionString,
            ValidatedBlobContainer = config.ValidatedBlobContainer,
            Hl7ResynchronizationContainer = config.Hl7ResynchronizationContainer,
            Hl7skippedContainer = config.Hl7skippedContainer
        };

        AppConfiguration appConfiguration = new()
        {
            HL7SequencingMaxParallelism = config.HL7SequencingMaxParallelism,
        };


        services.AddSingleton(blobConfig);
        services.AddSingleton(appConfiguration);
        services.AddTransient<ISequence, Sequence>();
        services.AddSingleton<PipeParser>();
        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(config.BlobConnectionString);
        });


    })
    .Build();

await host.RunAsync();