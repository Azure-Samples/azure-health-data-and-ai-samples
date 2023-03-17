using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HL7Sequencing.Model
{
    public class PostData
    {
        public string Hl7ArrayFileName {  get; set; }

        public bool ProceedOnError { get; set; }
    }
}
