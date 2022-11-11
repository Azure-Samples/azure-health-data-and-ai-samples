namespace UploadFhirJson.ProcessFhir
{
    public interface IFhirService
    {
        Task<HttpResponseMessage> Send(string reqBody);
    }
}
