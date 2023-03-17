namespace FHIRPostProcess.Configuration
{
    public class PostProcessConfiguration : BlobConfiguration
    {
        public string AppInsightConnectionstring { get; set; }

        public int MaxDegreeOfParallelism { get; set; }
    }
}
