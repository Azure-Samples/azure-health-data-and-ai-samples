using Azure.Storage.Blobs;
using FHIRPostProcess.Configuration;
using Hl7.Fhir.Serialization;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Xunit.Abstractions;
using Microsoft.Extensions.Logging.ApplicationInsights;
using FHIRPostProcess.PostProcessor;
using Azure.Core;
using FHIRPostProcess.Model;
using Newtonsoft.Json;

namespace FHIRPostProcess.Test
{
    public class FHIRPostProcessTest
    {
        private static PostProcessConfiguration? config;
        private static BlobConfiguration? blobConfig;
        private static AppConfiguration? appConfig;
        private static FhirJsonParser? parser;
        private static BlobServiceClient? blobServiceClient;
        private static TelemetryClient? telemetryClient;
        private static ILogger? logger;
        private static ITestOutputHelper? testContext;

        public FHIRPostProcessTest(ITestOutputHelper context)
        {
            testContext = context;
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");
            IConfigurationRoot root = builder.Build();
            config = new PostProcessConfiguration();
            root.Bind(config);


            if (config != null)
            {

                blobConfig = new()
                {
                    BlobConnectionString = config.BlobConnectionString,
                    Hl7ConverterJsonContainer = config.Hl7ConverterJsonContainer,
                    Hl7PostProcessContainer = config.Hl7PostProcessContainer
                };

                appConfig = new()
                {
                    MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
                };

                 parser = new();
                //change the parser settings to skip validations
                parser.Settings.AllowUnrecognizedEnums = true;
                parser.Settings.AcceptUnknownMembers = true;
                parser.Settings.PermissiveParsing = true;

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
                                     .BuildServiceProvider();

                var factory = serviceProvider.GetService<ILoggerFactory>();


                logger = factory.CreateLogger<PostProcess>();

                root.Bind(blobConfig);
                root.Bind(appConfig);
                root.Bind(parser);
                root.Bind(blobServiceClient);
                root.Bind(telemetryClient);
                root.Bind(logger);

            }
        }

        [Fact]
        public async Task FHIRPostProcess_Test()
        {
            string resultStatus = string.Empty;
            try
            {
                //code start for creating the sample request for FHIRPostProcess (Ensure HL7Files mentioned below must present in "hl7-converter-succeeded" storage container)
                string Hl7FileList = "[{\"HL7FileName\":\"ADT_A01-1_03092023083049.hl7\"},{\"HL7FileName\":\"ADT_A01-2_03092023083049.hl7\"}]";
                OrchestrationInput orchestrationInput = new();
                orchestrationInput.FhirBundleType = "Batch";
                orchestrationInput.Hl7Files = JsonConvert.DeserializeObject<List<Hl7File>>(Hl7FileList);
                //code end for creating the sample request for FHIRPostProcess

                IPostProcess postProcess = new PostProcess(blobConfig, appConfig, parser, blobServiceClient, telemetryClient, (ILogger<PostProcess>)logger);

                var result = await postProcess.PostProcessResources(orchestrationInput);
                if (!string.IsNullOrEmpty(result))
                {
                    try
                    {
                        List<Response> responses = JsonConvert.DeserializeObject<List<Response>>(result);
                        resultStatus = responses != null ? "Pass" : "Fail";
                        Assert.Equal("Pass", resultStatus);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail("Test method failed with exception:" + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                testContext?.WriteLine(ex.StackTrace);
            }
        }
    }
}