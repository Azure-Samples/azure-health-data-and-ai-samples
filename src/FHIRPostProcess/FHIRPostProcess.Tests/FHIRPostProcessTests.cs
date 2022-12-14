using FHIRPostProcess.PostProcessor;
using FHIRPostProcess.Tests.Assets;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace FHIRPostProcess.Tests
{
    public class FHIRPostProcessTests
    {
        private static ITestOutputHelper? _testContext;

        public FHIRPostProcessTests(ITestOutputHelper testContext)
        {
            _testContext = testContext;

        }

        //[Fact]
        //public async Task RemoveEmptyResources_BundleType_TransactionAsync()
        //{
        //    string fhirBundle = await File.ReadAllTextAsync("../../../TestData/bundleTransaction.json");
        //    try
        //    {
        //        PostProcess postProcess = new();
        //        FhirJsonParser _parser = new FhirJsonParser();
        //        Bundle bundle = _parser.Parse<Bundle>(fhirBundle);

        //        string requestUriString = "http://example.org/test";
        //        FunctionContext funcContext = new FakeFunctionContext();
        //        List<KeyValuePair<string, string>> headerList = new();
        //        headerList.Add(new KeyValuePair<string, string>("Accept", "application/json"));
        //        HttpHeadersCollection headers = new();
        //        MemoryStream reqstream = new MemoryStream(Encoding.UTF8.GetBytes(fhirBundle));
        //        HttpRequestData request = new FakeHttpRequestData(funcContext, "PUT", requestUriString, reqstream, headers);

        //        var postProcessResponse = await postProcess.PostProcessResources(request);

        //        string strPostProcessBundle = await new StreamReader(postProcessResponse.Body).ReadToEndAsync();
        //        Bundle postProcessBundle = _parser.Parse<Bundle>(strPostProcessBundle);
        //        Assert.NotNull(postProcessResponse);
        //        Assert.True(bundle.Entry.Count > postProcessBundle.Entry.Count);
        //        Assert.Equal(HttpStatusCode.OK, postProcessResponse.StatusCode);


        //    }
        //    catch (Exception ex)
        //    {
        //        _testContext?.WriteLine(ex.StackTrace);
        //    }
        //}

        //[Fact]
        //public async Task RemoveEmptyResources_BundleType_BatchAsync()
        //{
        //    string fhirBundle = await File.ReadAllTextAsync("../../../TestData/bundleBatch.json");
        //    try
        //    {
        //        PostProcess postProcess = new();
        //        FhirJsonParser _parser = new FhirJsonParser();
        //        Bundle bundle = _parser.Parse<Bundle>(fhirBundle);

        //        string requestUriString = "http://example.org/test";
        //        FunctionContext funcContext = new FakeFunctionContext();
        //        List<KeyValuePair<string, string>> headerList = new();
        //        headerList.Add(new KeyValuePair<string, string>("Accept", "application/json"));
        //        HttpHeadersCollection headers = new();
        //        MemoryStream reqstream = new MemoryStream(Encoding.UTF8.GetBytes(fhirBundle));
        //        HttpRequestData request = new FakeHttpRequestData(funcContext, "PUT", requestUriString, reqstream, headers);

        //        var postProcessResponse = await postProcess.PostProcessResources(request);

        //        string strPostProcessBundle = await new StreamReader(postProcessResponse.Body).ReadToEndAsync();
        //        Bundle postProcessBundle = _parser.Parse<Bundle>(strPostProcessBundle);

        //        Assert.NotNull(postProcessResponse);
        //        Assert.True(bundle.Type == Bundle.BundleType.Batch && postProcessBundle.Type == Bundle.BundleType.Transaction);
        //        Assert.True(bundle.Entry.Count > postProcessBundle.Entry.Count);
        //    }
        //    catch (Exception ex)
        //    {
        //        _testContext?.WriteLine(ex.StackTrace);
        //    }
        //}
    }
}