namespace UploadFhirJson.Model
{
    public class FhirDetails
    {
        public string HL7FileName { get; set; }
        public bool HL7Conversion { get; set; }
        public string FhirJson { get; set; }
    }

    public class FileName
    {
        public string HL7FileName { get; set; }
    }

    public class FhirInput
    {
        public List<FileName> sortedHL7files { get; set; }

        public List<FhirDetails> FhirData { get; set; }

        public bool proceedOnError { get; set; }
    }

    public class Response
    {
        public string FileName { get; set; }

        public int StatusCode { get; set; }

        public string Error { get; set; }

    }

    public class FhirResponse
    {
        public List<Response> success { get; set; } = new();

        public List<Response> failed { get; set; } = new();

        public List<Response> skipped { get; set; } = new();

    }

}
