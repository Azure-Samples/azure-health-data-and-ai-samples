using Azure.Storage.Blobs;
using HL7Sequencing.Configuration;
using HL7Sequencing.Model;
using HL7Sequencing.Sequencing;
using HL7Sequencing.Test.Model;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Newtonsoft.Json;
using NHapi.Base.Parser;
using System.Reflection;
using Xunit.Abstractions;

namespace HL7Sequencing.Test
{
    public class HL7SequencingTest
    {
        private static HL7SequencingConfiguration config;
        private static BlobConfig? blobConfig;
        private static AppConfiguration appConfiguration;
        private static PipeParser? parser;
        private static BlobServiceClient? blobServiceClient;
        private static TelemetryClient? telemetryClient;
        private static ILogger? logger;
        private static ITestOutputHelper? testContext;

        public HL7SequencingTest(ITestOutputHelper context)
        {
            testContext = context;
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");
            IConfigurationRoot root = builder.Build();
            config = new HL7SequencingConfiguration();
            root.Bind(config);

            if (config != null)
            {
                blobConfig = new()
                {
                    BlobConnectionString = config?.BlobConnectionString,
                    ValidatedBlobContainer = config?.ValidatedBlobContainer,
                    Hl7skippedContainer = config?.Hl7skippedContainer
                };

                appConfiguration = new()
                {
                    MaxDegreeOfParallelism = config.MaxDegreeOfParallelism
                };

                parser = new PipeParser();

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


                logger = factory.CreateLogger<Sequence>();

                root.Bind(blobConfig);
                root.Bind(appConfiguration);
                root.Bind(parser);
                root.Bind(blobServiceClient);
                root.Bind(telemetryClient);
                root.Bind(logger);
            }
        }

        [Fact]
        public async Task HL7Sequencing()
        {
            string resultStatus = string.Empty;
            try
            {

                //code start for creating the sample request for Hl7Sequencing.(Ensure Hl7ArrayFileName mentioned below must present in "hl7-validation-succeeded" storage container)
                string Hl7ArrayFileName = "Hl7FilesArray_03152023023406.json";
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(Hl7ArrayFileName);
                Hl7SequencingInput hl7SequencingInput = new();
                hl7SequencingInput.Hl7ArrayFileName = System.Convert.ToBase64String(plainTextBytes);
                hl7SequencingInput.ProceedOnError = true;
                var request = JsonConvert.SerializeObject(hl7SequencingInput);
                //code end for creating the sample request for Hl7Sequencing


                ISequence sequence = new Sequence(blobConfig, appConfiguration, blobServiceClient, parser, telemetryClient, (ILogger<Sequence>)logger);

                var result = await sequence.GetSequencListAsync(request);
                if (!string.IsNullOrEmpty(result))
                {
                    string AssertResult = string.Empty;
                    AssertResult = String.Equals(Hl7ArrayFileName, result) ? "Pass" : "Fail";
                    Assert.Equal("Pass", AssertResult);
                }
            }
            catch (Exception ex)
            {
                testContext?.WriteLine(ex.StackTrace);
            }
        }

    }
}