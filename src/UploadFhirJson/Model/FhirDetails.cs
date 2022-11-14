namespace UploadFhirJson.Model
{
    public class FhirDetails
    {
        public string HL7FileName { get; set; }
        public bool HL7Conversion { get; set; }
        public string FhirJson { get; set; }
    }
}
