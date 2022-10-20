using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace HL7Validation.Tests.Assets
{
    public class FakeHttpResponseData : HttpResponseData
    {
        public FakeHttpResponseData(FunctionContext context, HttpStatusCode code)
            : base(context)
        {
            StatusCode = code;
            Body = new MemoryStream();
            Headers = new HttpHeadersCollection();
        }

        public override HttpStatusCode StatusCode { get; set; }
        public override HttpHeadersCollection Headers { get; set; }
        public override Stream Body { get; set; }

        public override HttpCookies Cookies => throw new NotImplementedException();

    }
}
