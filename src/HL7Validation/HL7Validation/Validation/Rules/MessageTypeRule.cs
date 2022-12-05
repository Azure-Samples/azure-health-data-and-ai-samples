using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Util;
using NHapi.Base.Validation;
using System.Text.RegularExpressions;

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

            var terser = new Terser(message);
            if (string.IsNullOrEmpty(terser.Get("/MSH-9")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9 - Message Type should not be empty") };
            }

            if (string.IsNullOrEmpty(terser.Get("/MSH-9-1")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.1 - Message Code should not be empty") };
            }

            if (string.IsNullOrEmpty(terser.Get("/MSH-9-2")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.2 - Trigger Event should not be empty") };
            }

            if (terser.Get("/MSH-12").ToString() == "2.8" && string.IsNullOrEmpty(terser.Get("/MSH-9-3")))
            {
                validationResults = new ValidationException[1] { new ValidationException("MSH.9.3 - Message Structure should not be empty") };
            }

            if (!string.IsNullOrEmpty(terser.Get("/MSH-7")))
            {
                Regex dateTimeRegex = new Regex(@"^((?<year>\d{4})((?<month>\d{2})((?<day>\d{2})(?<time>((?<hour>\d{2})((?<minute>\d{2})((?<second>\d{2})(\.(?<millisecond>\d+))?)?)?))?)?)?(?<timeZone>(?<sign>-|\+)(?<timeZoneHour>\d{2})(?<timeZoneMinute>\d{2}))?)$");
                var isDateTimeValid = dateTimeRegex.IsMatch(terser.Get("/MSH-7"));
                if (!isDateTimeValid)
                {
                    validationResults = new ValidationException[1] { new ValidationException("MSH.7 - Message Structure should be valid") };
                }

            }

            return validationResults;
        }

        public ValidationException[] Test(IMessage msg)
        {
            throw new NotImplementedException();
        }
    }
}
