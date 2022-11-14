using HL7Sequencing.Configuration;
using HL7Sequencing.Sequencing;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
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
        BlobConfig blobConfig = new() { BlobConnectionString = config.BlobConnectionString, 
                                        ValidatedBlobContainer = config.ValidatedBlobContainer, 
                                        Hl7ResynchronizationContainer = config.Hl7ResynchronizationContainer};

        services.AddSingleton(blobConfig);
        services.AddTransient<ISequence,Sequence>();

    })
    .Build();

await host.RunAsync();
