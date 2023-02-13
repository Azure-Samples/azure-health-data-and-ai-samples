using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadFhirJson.Model
{
    public class ProcessRequest
    {
        public string FileList { get; set; }

        public int CurrentIndex { get; set; }

        public int BatchLimit { get; set; }

        public bool isFilesSkipped { get; set; }

        public bool FileProcessInSequence { get; set; }
    }
}
