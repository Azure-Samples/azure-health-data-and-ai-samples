namespace HL7Sequencing.Configuration
{
    public class HL7SequencingConfiguration
    {
        public string AppInsightConnectionstring { get; set; }

        public string BlobConnectionString { get; set; }

        public string ValidatedBlobContainer { get; set; }

        public string Hl7ResynchronizationContainer { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public string Hl7skippedContainer { get; set; }
    }
}
