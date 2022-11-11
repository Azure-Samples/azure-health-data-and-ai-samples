using Microsoft.Azure.Functions.Worker.Http;

namespace HL7Converter.ProcessConverter
{
    public interface IConverter
    {
        Task<HttpResponseData> Execute(HttpRequestData httpRequestData);
    }
}
