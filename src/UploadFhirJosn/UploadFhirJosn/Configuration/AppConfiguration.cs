using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UploadFhirJson.Configuration
{
    public class AppConfiguration
    {
        public string HttpFailStatusCodes { get; set; }

        public int UploadFhirJsonMaxParallelism { get; set; }
    }
}
