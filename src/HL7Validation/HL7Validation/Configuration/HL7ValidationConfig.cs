namespace Hl7Validation.Configuration
{
    public class HL7ValidationConfig
    {
        public string AppInsightConnectionstring { get; set; }

        public string BlobConnectionString { get; set; }

        public string ValidatedBlobContainer { get; set; }

        public string Hl7validationfailBlobContainer { get; set; }

        public string Hl7skippedContainer { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

    }
}
