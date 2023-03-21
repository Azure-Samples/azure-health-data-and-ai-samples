namespace FHIRPostProcess.Configuration
{
    public class PostProcessConfiguration : BlobConfiguration
    {
        public string AppInsightConnectionstring { get; set; }

        public int FHIRPostProcessMaxParallelism { get; set; }
    }
}
