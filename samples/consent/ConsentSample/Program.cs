using System.Reflection;
using Azure.Identity;
using ConsentSample.Configuration;
using ConsentSample.FhirOperation;
using ConsentSample.Filters;
using ConsentSample.Processors;
using Microsoft.AzureHealth.DataServices.Bindings;
using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Configuration;
using Microsoft.AzureHealth.DataServices.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;
#pragma warning disable CA1852
internal static class Program
{
    private static async Task Main(string[] args)
    {
        MyServiceConfig config = new();

        using IHost host = new HostBuilder()
            .ConfigureAppConfiguration((hostingContext, configuration) =>
            {
                configuration.Sources.Clear();

                IHostEnvironment env = hostingContext.HostingEnvironment;

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
                if (config.InstrumentationKey != null)
                {
                    services.UseAppInsightsLogging(config.InstrumentationKey, LogLevel.Information);
                    services.UseTelemetry(config.InstrumentationKey);
                }

                // Setup custom headers for use in an Input Filter
                services.UseCustomHeaders();
                services.AddCustomHeader("X-MS-AZUREFHIR-AUDIT-USER-TOKEN-TEST", "UseCaseSampleCustomOperation", CustomHeaderType.RequestStatic);

                // Setup pipeline for Azure function
                services.UseAzureFunctionPipeline();

                services.AddTransient<IFhirProcessor, FhirProcessor>();

                services.AddScoped<IFhirClient, FhirClient>();

                // Add our header modification as the first filter
                services.AddOutputFilter(typeof(ConsentSampleFilter));

                services.AddSingleton(config);

                // Add our binding to pass the call to the FHIR service
                services.AddBinding<RestBinding, RestBindingOptions>(options =>
                {
                    options.BaseAddress = config.FhirServerUrl;
                    options.Credential = new DefaultAzureCredential();
                });

                var credential = new DefaultAzureCredential();
                var baseUri = config.FhirServerUrl;
                string[]? scopes = default;

                services.AddHttpClient(config.SourceHttpClient, httpClient =>
                {
                    httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
                    httpClient.BaseAddress = baseUri;
                })
                .AddPolicyHandler(GetRetryPolicy())
                .AddHttpMessageHandler(x => new BearerTokenHandler(credential, baseUri, scopes));
            })
            .Build();

        await host.RunAsync();
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
