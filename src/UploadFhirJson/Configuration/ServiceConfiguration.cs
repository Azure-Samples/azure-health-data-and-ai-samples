namespace UploadFhirJson.Configuration
{
    public class ServiceConfiguration : BlobConfiguration
    {
        public string AppInsightConnectionstring { get; set; }

        public string FhirURL { get; set; }

        public string HttpFailStatusCodes { get; set; }

    }
}
