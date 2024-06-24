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


    public class BatchValidateModel
    {
        public List<CodingValidateEntry> Coding { get; set; }
        public BatchValidateModel()
        {
            Coding = new List<CodingValidateEntry>();
        }
    }

    public class CodingValidateEntry
    {
        public string code { get; set; }
        public string url { get; set; }
        public string date { get; set; }
        public string system { get; set; }
        public string valueSetVersion { get; set; }
        public bool? result { get; set; }
        public string message { get; set; }
        public string display { get; set; }


        
    }
}
