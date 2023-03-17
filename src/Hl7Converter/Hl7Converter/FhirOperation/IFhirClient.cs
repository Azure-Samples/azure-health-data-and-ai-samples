namespace HL7Converter.FhirOperation
{
    public interface IFhirClient
    {
        Task<HttpResponseMessage> Send(string reqBody, string hl7FileName);
    }
}
