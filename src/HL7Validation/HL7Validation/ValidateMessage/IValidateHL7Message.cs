using Microsoft.AspNetCore.Mvc;

namespace HL7Validation.ValidateMessage
{
    public interface IValidateHL7Message
    {
        Task<ContentResult> ValidateMessage(string hl7Message);

    }
}
