using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FHIRPostProcess.Model
{
    public class PostProcessInput
    {
        public string HL7FileName { get; set; }
        public bool HL7Conversion { get; set; }
        public string FhirJson { get; set; }

    }
}
