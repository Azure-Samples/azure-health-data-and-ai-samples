using HL7Validation.CustomValidation.Rules;
using NHapi.Base.Validation.Implementation;

namespace HL7Validation.Validation
{
    internal sealed class CustomValidation : StrictValidation
    {
        public CustomValidation()
        {
            var messageTypeRule = new MessageTypeRule();
            MessageRuleBindings.Add(new RuleBinding("*", "*", messageTypeRule));
        }
    }
}
