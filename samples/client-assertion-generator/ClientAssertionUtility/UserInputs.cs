using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClientAssertionUtility
{
    public class UserInputs
    {
        public string ClientID { get; set; }
        public string TenantID { get; set; }
        public string CertThumbPrint { get; set; }
        public string KeyFilePath { get; set; }
        public string AudURL { get; set; }

    }
}
