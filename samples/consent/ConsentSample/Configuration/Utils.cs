using Microsoft.AspNetCore.Http;
using Microsoft.AzureHealth.DataServices.Pipelines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace ConsentSample.Configuration
{
    public class Utils
    {
        public static readonly string FHIR_PROXY_ROLES = "fhirproxy-roles";
        public static bool inServerAccessRole(OperationContext req, string role)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FP-AUTHFREEPASS"))) return true;

            var headers = req.Request.Headers;
            IEnumerable<string> values;
            if (headers.TryGetValues("fhirproxy-roles", out values))
            {
                string val = values.First();
                return val.Contains(role);
                        
            }
            else
                return false;
        }

        public static string genOOErrResponse(string code, string desc)
        {

            return $"{{\"resourceType\": \"OperationOutcome\",\"id\": \"{Guid.NewGuid().ToString()}\",\"issue\": [{{\"severity\": \"error\",\"code\": \"{code ?? ""}\",\"diagnostics\": \"{desc ?? ""}\"}}]}}";

        }
    }
}
