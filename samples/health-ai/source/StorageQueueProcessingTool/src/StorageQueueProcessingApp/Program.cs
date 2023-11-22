using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StorageQueueProcessingApp.Configuration;
using System.Reflection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Extensions.Logging;
using StorageQueueProcessingApp.FHIRClient;
using StorageQueueProcessingApp.Processors;
using StorageQueueProcessingApp.DICOMClient;
using Polly.Extensions.Http;
using Polly;
using Microsoft.Net.Http.Headers;
using StorageQueueProcessingApp.Security;

public class Program
{
	private static void Main(string[] args)
	{
		ProcessorConfig  config = new ProcessorConfig();
		var host = new HostBuilder()
			.ConfigureAppConfiguration((hostingContext, configuration) =>
			{
				configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
				.AddUserSecrets(Assembly.GetExecutingAssembly(), true)
				.AddEnvironmentVariables("");

				IConfigurationRoot configurationRoot = configuration.Build();
				configurationRoot.Bind(config);
			})
			.ConfigureFunctionsWorkerDefaults()
			.ConfigureServices(services =>
			{
				var credential = new DefaultAzureCredential();
				if (config.AppInsightConnectionstring != null)
				{
					services.AddLogging(builder =>
					{
						builder.AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, config.Debug ? LogLevel.Debug : LogLevel.Information);
						builder.AddApplicationInsights(op => op.ConnectionString = config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
					});

					services.Configure<TelemetryConfiguration>(options =>
					{
						options.ConnectionString = config.AppInsightConnectionstring;
					});
					services.AddTransient<TelemetryClient>();
				}

				services.AddTransient<IFHIRProcessor, FHIRProcessor>();
				services.AddTransient<IDICOMProcessor, DICOMProcessor>();
				services.AddSingleton<IBlobProcessor, BlobProcessor>();
				services.AddScoped<IFHIRClient, FHIRClient>();
				services.AddScoped<IDICOMClient, DICOMClient>();
				services.AddSingleton(config);

				var fhirUri = config.FhirUri;
				string[]? scopes = default;

				services.AddHttpClient(config.FhirHttpClient, httpClient =>
				{
					httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
					httpClient.BaseAddress = fhirUri;
				})
				.AddPolicyHandler(GetRetryPolicy())
				.AddHttpMessageHandler(x => new BearerTokenHandler(credential, fhirUri, scopes));

				services.AddHttpClient(config.DicomHttpClient, httpClient =>
				{
					httpClient.DefaultRequestHeaders.Add(HeaderNames.UserAgent, config.UserAgent);
					httpClient.BaseAddress = config.DicomUri;
				})
				.AddPolicyHandler(GetRetryPolicy())
				.AddHttpMessageHandler(x => new BearerTokenHandler(credential, config.DicomResourceUri, scopes));
			})
			.Build();

		host.Run();

	}

	private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
	{
		return HttpPolicyExtensions
			.HandleTransientHttpError()
			.OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
			.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
	}
}
