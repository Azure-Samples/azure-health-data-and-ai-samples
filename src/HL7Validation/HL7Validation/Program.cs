using HL7Validation.Configuration;
using HL7Validation.ValidateMessage;
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

HL7ValidationConfig config = new();

using IHost host = new HostBuilder()
    .ConfigureAppConfiguration((hostingContext, configuration) =>
    {
        configuration.Sources.Clear();

        configuration
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
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
            Hl7validationfailBlobContainer = config.Hl7validationfailBlobContainer
        };

        services.AddSingleton(blobConfig);
        services.AddTransient<IValidateHL7Message, ValidateHL7Message>();
        var parser = new PipeParser { ValidationContext = new HL7Validation.Validation.CustomValidation() };
        services.AddSingleton(parser);

        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(config.BlobConnectionString);
        });


    })
    .Build();

await host.RunAsync();
