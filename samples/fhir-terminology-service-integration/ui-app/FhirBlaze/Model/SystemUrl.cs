using Hl7.Fhir.Model;
using System.Collections.Generic;

namespace FhirBlaze.Model
{
    public class SystemUrl
    {
        public List<CodeSystemModel> LookupCodeSystemUrlList { get; set; }

        public List<CodeSystemModel> TransLateCodeSystemUrlList { get; set; }
    }
}
