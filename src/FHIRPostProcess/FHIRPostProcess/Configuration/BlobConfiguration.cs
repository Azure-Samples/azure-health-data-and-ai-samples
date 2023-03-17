namespace FHIRPostProcess.Configuration
{
    public class BlobConfiguration
    {
        public string BlobConnectionString { get; set; }
        public string Hl7ConverterJsonContainer { get; set;}
        public string Hl7PostProcessContainer { get; set;}
        public string ValidatedContainer { get; set;}
    }
}
