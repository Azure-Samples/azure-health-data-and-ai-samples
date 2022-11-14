namespace UploadFhirJson.Model
{
    public class BlobConfiguration
    {
        public string BlobConnectionString { get; set; }

        public string SuccessBlobContainer { get; set; }

        public string HL7FailedBlob { get; set; }

        public string FailedBlobContainer { get; set; }

        public string FhirFailedBlob { get; set; }

        public string ValidatedBlobContainer { get; set; }

        public string SkippedBlobContainer { get; set; }
    }
}
