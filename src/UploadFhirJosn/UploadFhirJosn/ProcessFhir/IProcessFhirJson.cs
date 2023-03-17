using Microsoft.Azure.Functions.Worker.Http;

namespace UploadFhirJson.ProcessFhir
{
    public interface IProcessFhirJson
    {
        Task<string> Execute(string requestData);
    }
}
