namespace FHIRPostProcess.Model
{
    public class PostProcessOutput
    {
        public string HL7FileName { get; set; }
        public bool HL7Conversion { get; set; }
        public string FhirJson { get; set; }

    }

    public class Response
    {
        public string FileName { get; set; }

        public string Error { get; set; }
    }
}
