using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Util;
using NHapi.Base.Validation;
using NHapi.Model.V28.Segment;

namespace HL7Validation.CustomValidation.Rules
{
    public class MessageTypeRule : IMessageRule
    {
        public virtual string Description => string.Empty;

        public virtual string SectionReference => String.Empty;


        public ValidationException[] test(IMessage message)
        {
            var validationResults = new ValidationException[0];
            var encodingChars = new EncodingCharacters('|', null);
            var msh = (MSH)message.GetStructure("MSH");
            var messageType = PipeParser.Encode(msh, encodingChars).Split('|');
            if (string.IsNullOrEmpty(messageType[8]))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9 - Message Type should not be empty") };
            }

            //check the segment value is empty.
            var terser = new Terser(message);
            if (string.IsNullOrEmpty(terser.Get("/MSH-9-1")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.1 - Message Code should not be empty") };
            }

            if (string.IsNullOrEmpty(terser.Get("/MSH-9-2")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.2 - Trigger Event should not be empty") };
            }

            if (string.IsNullOrEmpty(terser.Get("/MSH-9-3")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.3 - Message Structure should not be empty") };
            }

            return validationResults;
        }

        public ValidationException[] Test(IMessage msg)
        {
            throw new NotImplementedException();
        }
    }
}
