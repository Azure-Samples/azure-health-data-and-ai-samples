

namespace HL7Validation.Model
{
    public class ResponseData
    {
        public IEnumerable<object> Success { get; set; }

        public IEnumerable<object> Fail { get; set; }
    }
}
