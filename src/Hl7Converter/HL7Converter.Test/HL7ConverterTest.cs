using Azure.Storage.Blobs;
using HL7Converter.Caching;
using HL7Converter.Configuration;
using HL7Converter.FhirOperation;
using HL7Converter.Model;
using HL7Converter.ProcessConverter;
using HL7Converter.Test.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sprache;
using System.Reflection;
using Xunit.Abstractions;

namespace HL7Converter.Test
{
    public class HL7ConverterTest
    {
        private static ServiceConfiguration? config;
        private static BlobConfiguration? blobConfig;
        private static AppConfiguration? appConfig;
        private static BlobServiceClient? blobServiceClient;
        private static TelemetryClient? telemetryClient;
        private static IFhirClient? fhirClient;
        private static ILogger? logger;
        private static ITestOutputHelper? testContext;


        public HL7ConverterTest(ITestOutputHelper context)
        {
            testContext = context;
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");
            IConfigurationRoot root = builder.Build();
            config = new ServiceConfiguration();
            root.Bind(config);

            if (config != null)
            {
                blobConfig = new()
                {
                    BlobConnectionString = config.BlobConnectionString,
                    ConvertedContainer = config.ConvertedContainer,
                    ValidatedContainer = config.ValidatedContainer,
                    ConversionfailContainer = config.ConversionfailContainer,
                    Hl7ConverterJsonContainer = config.Hl7ConverterJsonContainer,
                    SkippedBlobContainer = config.SkippedBlobContainer
                };

                appConfig = new()
                {
                    HttpFailStatusCodes = config.HttpFailStatusCodes,
                    MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
                };


                blobServiceClient = new BlobServiceClient(config.BlobConnectionString);

                TelemetryConfiguration telemetryConfiguration = new TelemetryConfiguration();
                telemetryConfiguration.ConnectionString = config.AppInsightConnectionstring;
                telemetryClient = new TelemetryClient(telemetryConfiguration);

                var serviceProvider = new ServiceCollection()
                                     .AddLogging(builder =>
                                     {
                                         builder.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
                                         builder.AddApplicationInsights(op => op.ConnectionString = config.AppInsightConnectionstring, op => op.FlushOnDispose = true);
                                     })
                                     .AddMemoryCache()
                                     .AddHttpClient()
                                     .AddScoped<IAuthTokenCache, AuthTokenCache>()
                                     .AddScoped<IFhirClient, FhirClient>()
                                     .BuildServiceProvider();

                var factory = serviceProvider.GetService<ILoggerFactory>();

                logger = factory.CreateLogger<Converter>();
                var fhirLogger = factory.CreateLogger<FhirClient>();

                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                var memoryCache = serviceProvider.GetService<IAuthTokenCache>();

                fhirClient = new FhirClient(config, httpClientFactory, memoryCache, fhirLogger);

                root.Bind(blobConfig);
                root.Bind(appConfig);
                root.Bind(fhirClient);
                root.Bind(blobServiceClient);
                root.Bind(telemetryClient);
                root.Bind(logger);
            }
        }

        [Fact]
        public async Task HL7Conversion_Test()
        {
            string resultStatus = string.Empty;
            try
            {
                //code start for creating the sample request for Hl7conversion.(Ensure Hl7ArrayFileName mentioned below must present in "hl7-validation-succeeded" storage container)
                string Hl7ArrayFileName = "Hl7FilesArray_03152023023406.json";
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(Hl7ArrayFileName);
                Hl7ConverterRequest hl7ConverterRequest = new();
                hl7ConverterRequest.Hl7ArrayFileName = System.Convert.ToBase64String(plainTextBytes);
                hl7ConverterRequest.ProceedOnError = true;
                var request = JsonConvert.SerializeObject(hl7ConverterRequest);
                //code end for creating the sample request for Hl7conversion

                IConverter converter = new Converter(blobConfig, appConfig, fhirClient, blobServiceClient, telemetryClient, (ILogger<Converter>)logger);
                try
                {
                    string result = "";
                    await converter.Execute(request);
                    
                    if (!string.IsNullOrEmpty(result))
                    {
                        try
                        {
                            Hl7ConverterResponse responseData = JsonConvert.DeserializeObject<Hl7ConverterResponse>(result);
                            resultStatus = "Pass";
                        }
                        catch (Exception ex)
                        {
                            Assert.Fail("Test method failed with exception:" + ex.Message);
                        }
                    }

                    Assert.Equal("Pass", resultStatus);
                }
                catch (Exception ex)
                {
                    Assert.Fail("Test method failed with exception:" + ex.Message);
                }
            }
            catch (Exception ex)
            {
                testContext?.WriteLine(ex.StackTrace);
            }
        }
    }
}