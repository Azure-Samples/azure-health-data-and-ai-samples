using Hl7.Fhir.Model;
using System.Collections.Generic;

namespace FhirBlaze.Model
{
    public class BatchTranslateModel
    {
        public List<CodingEntry> Coding { get; set; }
        public BatchTranslateModel()
        {
            Coding = new List<CodingEntry>();
        }
    }
   
    public class CodingEntry
    {
        public string code { get; set; }
        public string system { get; set; }
        public string targetsystem { get; set; }
        public string display { get; set; }
    }
}
