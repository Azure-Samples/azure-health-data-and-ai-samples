namespace HL7Sequencing.Configuration
{
    public class BlobConfig
    {
        public string? BlobConnectionString { get; set; }

        public string? ValidatedBlobContainer { get; set; }

        public string? Hl7ResynchronizationContainer { get; set; }

    }
}
