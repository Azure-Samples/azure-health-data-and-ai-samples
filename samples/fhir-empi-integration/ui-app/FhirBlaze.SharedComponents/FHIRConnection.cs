using System;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents
{
    public class FHIRConnection
    {
        public const string FHIRClient = "FHIRClient";
        public string ClientId { get; set; } = string.Empty;

        public string Secret { get; set; } = string.Empty;

        public string Resource { get; set; } = string.Empty;

        public string Tenant { get; set; } = string.Empty;

    }
}
