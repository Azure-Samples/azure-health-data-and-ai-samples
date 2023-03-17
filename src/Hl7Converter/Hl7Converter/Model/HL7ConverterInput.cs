using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HL7Converter.Model
{
    public class HL7ConverterInput
    {
        public string Hl7ArrayFileName { get; set; }
        public bool ProceedOnError { get; set; }

    }

    public class Hl7File
    {
        public string HL7FileName { get; set; }
        public string HL7FileType { get; set; }

    }
}
