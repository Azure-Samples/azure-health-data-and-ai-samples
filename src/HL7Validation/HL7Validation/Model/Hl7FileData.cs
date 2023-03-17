using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hl7Validation.Model
{
    public class Hl7FileData
    {
        public List<string> Hl7files { get; set; }
        public int MaxDegreeOfParallelism { get; set; }
        public string containerName { get; set; }

        public bool proceedOnError { get; set; }
    }
}
