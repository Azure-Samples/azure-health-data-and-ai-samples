using HL7Validation.Configuration;
using HL7Validation.Parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Xunit.Abstractions;

namespace HL7Validation.Test
{
    public class ValidateHL7Test
    {
        private static MyServiceConfig? config;
        private static ITestOutputHelper? testContext;

        public ValidateHL7Test(ITestOutputHelper context)
        {
            testContext = context;
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
                .AddEnvironmentVariables("AZURE_");
            IConfigurationRoot root = builder.Build();
            config = new MyServiceConfig();
            root.Bind(config);
        }
        [Fact]
        public async static Task ValidateHL7Message_Success()
        {
            string message = await File.ReadAllTextAsync(@"../../../TestSample/ADT_A01_Pid_CX1.hl7");
            try
            {
                IValidateHL7Message validateHL7Message = new ValidateHL7Message();
                ContentResult contentResult = await validateHL7Message.ValidateMessage(message);
                Assert.Equal(200, contentResult.StatusCode);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.StackTrace);
            }
        }

        [Fact]
        public async static Task ValidateHL7Message_Fail()
        {
            string message = await File.ReadAllTextAsync(@"../../../TestSample/ADT_A01_Pid.hl7");
            try
            {
                IValidateHL7Message validateHL7Message = new ValidateHL7Message();
                ContentResult contentResult = await validateHL7Message.ValidateMessage(message);
                Assert.Equal(500, contentResult.StatusCode);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.StackTrace);
            }
        }

    }
}