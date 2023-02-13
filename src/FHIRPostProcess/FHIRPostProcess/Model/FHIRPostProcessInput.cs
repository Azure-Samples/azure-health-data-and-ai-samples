using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FHIRPostProcess.Model
{
    public class FHIRPostProcessInput
    {
        public string Hl7FilesList { get; set; }
        public string FhirBundleType { get; set; }
        public int Take { get; set; }
        public int Skip { get; set; }
    }
    

}
