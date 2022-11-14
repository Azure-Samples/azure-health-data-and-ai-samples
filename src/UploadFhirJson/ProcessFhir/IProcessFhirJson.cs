using Microsoft.Azure.Functions.Worker.Http;

namespace UploadFhirJson.ProcessFhir
{
    public interface IProcessFhirJson
    {
        Task<HttpResponseData> Execute(HttpRequestData httpRequestData);
    }
}
