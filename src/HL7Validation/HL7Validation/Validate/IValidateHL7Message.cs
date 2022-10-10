using Microsoft.AspNetCore.Mvc;

namespace HL7Validation.Parser
{
    public interface IValidateHL7Message
    {
        Task<ContentResult> ValidateMessage(string hl7Message);

    }
}
