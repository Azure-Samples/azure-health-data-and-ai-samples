using FHIRPostProcess.Configuration;
using FHIRPostProcess.PostProcessor;
using Hl7.Fhir.Serialization;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using System.Reflection;

PostProcessConfiguration config = new();

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
            Hl7ConverterJsonContainer = config.Hl7ConverterJsonContainer,
            Hl7PostProcessContainer = config.Hl7PostProcessContainer,
            ValidatedContainer = config.ValidatedContainer,
        };

        services.AddSingleton(blobConfig);
        services.AddTransient<IPostProcess, PostProcess>();

        AppConfiguration appConfig = new()
        {            
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
        };
        services.AddSingleton(appConfig);


        FhirJsonParser _parser = new();
        //change the parser settings to skip validations
        _parser.Settings.AllowUnrecognizedEnums = true;
        _parser.Settings.AcceptUnknownMembers = true;
        _parser.Settings.PermissiveParsing = true;

        services.AddSingleton(_parser);

        services.AddAzureClients(clientBuilder =>
        {
            clientBuilder.AddBlobServiceClient(config.BlobConnectionString);
        });

    })
    .ConfigureLogging(e =>
    {
        e.AddEventSourceLogger();
    })
    .Build();

await host.RunAsync();