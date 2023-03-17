namespace FHIRPostProcess.Model
{
    public class OrchestrationInput
    {
        public List<Hl7File> Hl7Files { get; set; }

        public string FhirBundleType { get; set; }
    }
}
