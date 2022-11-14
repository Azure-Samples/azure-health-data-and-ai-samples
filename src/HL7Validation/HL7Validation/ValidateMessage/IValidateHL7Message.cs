using Microsoft.Azure.Functions.Worker.Http;

namespace HL7Validation.ValidateMessage
{
    public interface IValidateHL7Message
    {
        Task<HttpResponseData> ValidateMessage(HttpRequestData request);
    }
}
