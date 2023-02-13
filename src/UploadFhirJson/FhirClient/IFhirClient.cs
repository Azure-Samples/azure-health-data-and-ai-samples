namespace UploadFhirJson.FhirClient
{
    public interface IFhirClient
    {
        Task<HttpResponseMessage> Send(string reqBody, string hl7FileName);
    }
}
