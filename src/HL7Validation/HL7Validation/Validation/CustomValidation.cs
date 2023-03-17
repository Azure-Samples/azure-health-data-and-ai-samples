using Hl7Validation.CustomValidation.Rules;
using NHapi.Base.Validation.Implementation;

namespace Hl7Validation.Validation
{
    public class CustomValidation : StrictValidation
    {
        public CustomValidation()
        {
            var messageTypeRule = new MessageTypeRule();
            MessageRuleBindings.Add(new RuleBinding("*", "*", messageTypeRule));

        }
    }
}
