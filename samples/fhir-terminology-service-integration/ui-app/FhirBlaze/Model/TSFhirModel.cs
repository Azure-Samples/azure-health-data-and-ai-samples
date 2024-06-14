using System.Collections.Generic;

namespace FhirBlaze.Model
{
    public class TSFhirModel
    {

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;

        public string LookupCodeValue { get; set; } = string.Empty;

        public string LookupCodeSystem { get; set; } = string.Empty;

        public string TranslateCodeValue { get; set; } = string.Empty;

        public string TranslateTargetCodeSystem { get; set; } = string.Empty;

        public string TranslateSourceCodeSystem { get; set; } = string.Empty;

        public string observationJson { get; set; } = string.Empty;

        public string LookUpAndTranslateJson { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public List<string> Codelist { get; set; }  = new List<string>();

    }
}
