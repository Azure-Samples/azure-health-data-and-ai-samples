

namespace Hl7Validation.Model
{
    public class ResponseData
    {
        public string Success { get; set; }

        public IEnumerable<object> Fail { get; set; }

    }
}
