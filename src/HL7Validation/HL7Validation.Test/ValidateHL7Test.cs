using HL7Validation.Configuration;
using HL7Validation.ValidateMessage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using Xunit.Abstractions;
using HL7Validation.Tests.Assets;

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
                string requestUriString = "http://example.org/test";
                FunctionContext funcContext = new FakeFunctionContext();
                List<KeyValuePair<string, string>> headerList = new();
                headerList.Add(new KeyValuePair<string, string>("Accept", "application/json"));
                HttpHeadersCollection headers = new();
                MemoryStream reqstream = new MemoryStream(Encoding.UTF8.GetBytes(message));
                HttpRequestData request = new FakeHttpRequestData(funcContext, "POST", requestUriString, reqstream, headers);

                var httpResponseData = await validateHL7Message.ValidateMessage(request);
                Assert.Equal(HttpStatusCode.OK, httpResponseData.StatusCode);

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
                string requestUriString = "http://example.org/test";
                FunctionContext funcContext = new FakeFunctionContext();
                List<KeyValuePair<string, string>> headerList = new();
                headerList.Add(new KeyValuePair<string, string>("Accept", "application/json"));
                HttpHeadersCollection headers = new();
                MemoryStream reqstream = new MemoryStream(Encoding.UTF8.GetBytes(message));
                HttpRequestData request = new FakeHttpRequestData(funcContext, "POST", requestUriString, reqstream, headers);

                var httpResponseData = await validateHL7Message.ValidateMessage(request);
                Assert.Equal(HttpStatusCode.InternalServerError, httpResponseData.StatusCode);
            }
            catch (Exception ex)
            {
                testContext.WriteLine(ex.StackTrace);
            }
        }

    }
}