namespace HL7Converter.Configuration
{
    public class ServiceConfiguration : BlobConfiguration
    {
        public string AppInsightConnectionstring { get; set; }

        public string FhirURL { get; set; }

        public string HttpFailStatusCodes { get; set; }

        public int HL7ConverterMaxParallelism { get; set; }

    }
}
