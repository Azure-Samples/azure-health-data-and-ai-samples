namespace UploadFhirJson.Model
{
    internal class FhirResponse
    {
        public List<Response> response { get; set; } = new();
        public bool IsFileSkipped { get; set; }

    }
}
