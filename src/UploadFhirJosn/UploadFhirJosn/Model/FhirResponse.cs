namespace UploadFhirJson.Model
{
    public class FhirResponse
    {
        public List<Response> response { get; set; } = new();
        public bool IsFileSkipped { get; set; }

    }
}
