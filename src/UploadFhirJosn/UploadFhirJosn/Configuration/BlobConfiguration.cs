using DurableTask.Core;

namespace UploadFhirJson.Configuration
{
    public class BlobConfiguration
    {
        public string BlobConnectionString { get; set; }

        public string ProcessedBlobContainer { get; set; }

        public string HL7FailedBlob { get; set; }

        public string FailedBlobContainer { get; set; }

        public string FhirFailedBlob { get; set; }

        public string ConvertedContainer { get; set; }

        public string SkippedBlobContainer { get; set; }

        public string FhirJsonContainer { get; set; }

        public string HL7FhirPostPorcessJson { get; set; }

        public string ValidatedContainer { get; set; }
    }
}
