namespace Hl7Validation.Configuration
{
    public class BlobConfig
    {
        public string BlobConnectionString { get; set; }

        public string ValidatedBlobContainer { get; set; }

        public string Hl7validationfailBlobContainer { get; set; }

        public string Hl7skippedContainer { get; set; }
    }
}
