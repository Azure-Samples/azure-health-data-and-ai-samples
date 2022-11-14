namespace UploadFhirJson.Model
{
    public class FhirInput
    {
        public List<FileName> sortedHL7files { get; set; }

        public List<FhirDetails> FhirData { get; set; }

        public bool proceedOnError { get; set; }
    }
}
