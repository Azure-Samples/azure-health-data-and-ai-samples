namespace UploadFhirJson.Model
{
    internal class FhirResponse
    {
        public List<Response> success { get; set; } = new();

        public List<Response> failed { get; set; } = new();

        public List<Response> skipped { get; set; } = new();

    }
}
