using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HL7Sequencing.Test.Model
{
    public class Hl7SequencingInput
    {
        public string Hl7ArrayFileName { get; set; }
        public bool ProceedOnError { get; set; }
    }
}
