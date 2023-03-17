using Hl7Validation.Configuration;
using Hl7Validation.ValidateMessage;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Xunit.Abstractions;
using NHapi.Base.Parser;
using Azure.Storage.Blobs;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.ApplicationInsights;
using Hl7Validation.Model;
using Newtonsoft.Json;

namespace HL7Validation.Test
{
    public class ValidateHL7Test
    {
        private static HL7ValidationConfig? config;
        private static BlobConfig? blobConfig;
        private static AppConfiguration? appConfiguration;
        private static PipeParser? parser;
        private static BlobServiceClient? blobServiceClient;
        private static TelemetryClient? telemetryClient;
        private static ILogger? logger;
        private static ITestOutputHelper? testContext;


        public ValidateHL7Test(ITestOutputHelper context)
        {
            
            testContext = context;
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");
            IConfigurationRoot root = builder.Build();
            config = new HL7ValidationConfig();
            root.Bind(config);

            if (config != null)
            {
                blobConfig = new()
                {
                    BlobConnectionString = config?.BlobConnectionString,
                    ValidatedBlobContainer = config?.ValidatedBlobContainer,
                    Hl7validationfailBlobContainer = config?.Hl7validationfailBlobContainer
                };

                appConfiguration = new()
                {
                    MaxDegreeOfParallelism = config.MaxDegreeOfParallelism
                };

                parser = new PipeParser { ValidationContext = new Hl7Validation.Validation.CustomValidation() };

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


                logger = factory.CreateLogger<ValidateHL7Message>();

                root.Bind(blobConfig);
                root.Bind(appConfiguration);
                root.Bind(parser);
                root.Bind(blobServiceClient);
                root.Bind(telemetryClient);
                root.Bind(logger);
            }

        }

        [Fact]
        public async Task ValidateHL7Message_Success()
        {
            string resultStatus = string.Empty;
            try
            {
                //Upload Hl7File to your storage container.
                string request = "{\"ContainerName\":\"hl7filesinput\",\"ProceedOnError\":true}";
                IValidateHL7Message validateHL7Message = new ValidateHL7Message(blobConfig, appConfiguration, parser, blobServiceClient, telemetryClient, (ILogger<ValidateHL7Message>)logger);

                var result = await validateHL7Message.ValidateMessage(request);
                if (!string.IsNullOrEmpty(result))
                {
                    try
                    {
                        ResponseData responseData = JsonConvert.DeserializeObject<ResponseData>(result);
                        resultStatus = responseData.Success != null && responseData.Fail != null ? "Pass" : "Fail";
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