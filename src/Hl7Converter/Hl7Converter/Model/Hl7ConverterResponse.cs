using Azure;

namespace HL7Converter.Model
{
    public class Hl7ConverterResponse
    {
        public List<Response> response { get; set; } = new();
        public bool IsFileSkipped { get; set; }

    }
}
