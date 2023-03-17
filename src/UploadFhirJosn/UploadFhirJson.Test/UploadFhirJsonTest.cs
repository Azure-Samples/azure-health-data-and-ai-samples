using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using System.Reflection;
using UploadFhirJson.Caching;
using UploadFhirJson.Configuration;
using UploadFhirJson.FhirOperation;
using UploadFhirJson.Model;
using UploadFhirJson.ProcessFhir;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace UploadFhirJson.Test
{
    public class UploadFhirJsonTest
    {

        private static ServiceConfiguration? config;
        private static BlobConfiguration? blobConfig;
        private static AppConfiguration? appConfiguration;
        private static TelemetryClient? telemetryClient;
        private static IFhirClient? fhirClient;
        private static ILogger? logger;
        private static ITestOutputHelper? testContext;
        public UploadFhirJsonTest(ITestOutputHelper context)
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
                    ProcessedBlobContainer = config.ProcessedBlobContainer,
                    FhirFailedBlob = config.FhirFailedBlob,
                    HL7FailedBlob = config.HL7FailedBlob,
                    SkippedBlobContainer = config.SkippedBlobContainer,
                    ConvertedContainer = config.ConvertedContainer,
                    FailedBlobContainer = config.FailedBlobContainer,
                    FhirJsonContainer = config.FhirJsonContainer,
                    HL7FhirPostPorcessJson = config.HL7FhirPostPorcessJson,
                    ValidatedContainer = config.ValidatedContainer,
                };

                appConfiguration = new()
                {
                    HttpFailStatusCodes = config.HttpFailStatusCodes,
                    MaxDegreeOfParallelism = config.MaxDegreeOfParallelism

                };


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

                logger = factory.CreateLogger<ProcessFhirJson>();
                var fhirLogger = factory.CreateLogger<FhirClient>();

                var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
                var memoryCache = serviceProvider.GetService<IAuthTokenCache>();

                fhirClient = new FhirClient(config, httpClientFactory, memoryCache, fhirLogger);


                root.Bind(blobConfig);
                root.Bind(appConfiguration);
                root.Bind(fhirClient);
                root.Bind(telemetryClient);
                root.Bind(logger);
            }
        }

        [Fact]
        public async Task UploadFhirJson_Test()
        {
            string resultStatus = string.Empty;
            try
            {
                //code start for creating the sample request for UploadFhirJson.(Ensure Hl7ArrayFileName mentioned below must present in "hl7-validation-succeeded" storage container)
                string Hl7ArrayFileName = "Hl7FilesArray_03152023023406.json";
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(Hl7ArrayFileName);
                ProcessRequest processRequest = new();
                processRequest.Hl7ArrayFileName = System.Convert.ToBase64String(plainTextBytes);
                processRequest.ProceedOnError = true;
                processRequest.FileProcessInSequence = false;
                processRequest.SkipFhirPostProcess = false;
                var request = JsonConvert.SerializeObject(processRequest);

                //code end for creating the sample request for UploadFhirJson.

                IProcessFhirJson processFhirJson = new ProcessFhirJson(blobConfig, appConfiguration, fhirClient,telemetryClient, (ILogger<ProcessFhirJson>)logger);
                try
                {
                    string result = string.Empty;
                    result = await processFhirJson.Execute(request);

                    if (!string.IsNullOrEmpty(result))
                    {
                        try
                        {
                            FhirResponse fhirResponse = JsonConvert.DeserializeObject<FhirResponse>(result);
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