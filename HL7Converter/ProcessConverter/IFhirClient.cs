namespace HL7Converter.ProcessConverter
{
    public interface IFhirClient
    {
        Task<HttpResponseMessage> Send(string reqBody);
    }
}
