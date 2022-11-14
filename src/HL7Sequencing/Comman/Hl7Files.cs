namespace HL7Sequencing.Comman
{
    public class Hl7Files
    {
        public string? HL7FileName { get; set; }

        public string? SequenceNumber { get; set; }

        public string? HL7FileType { get; set; }

        public DateTimeOffset DateTimeOffsetOfMessage { get; set; }
    }
}
