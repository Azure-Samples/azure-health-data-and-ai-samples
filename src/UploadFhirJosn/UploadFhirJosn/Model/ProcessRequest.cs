using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadFhirJson.Model
{
    public class ProcessRequest
    {
        public string Hl7ArrayFileName { get; set; }
        public bool ProceedOnError { get; set; }

        public bool FileProcessInSequence { get; set; }

        public bool SkipFhirPostProcess { get; set; }
    }
}
