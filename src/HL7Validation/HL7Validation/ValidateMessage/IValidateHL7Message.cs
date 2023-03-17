using Microsoft.Azure.Functions.Worker.Http;

namespace Hl7Validation.ValidateMessage
{
    public interface IValidateHL7Message
    {
        Task<string> ValidateMessage(string request);
    }
}
