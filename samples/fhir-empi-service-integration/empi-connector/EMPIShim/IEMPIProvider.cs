using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMPIShim
{
    public interface IEMPIProvider
    {
        public Task<MatchResult> RunMatch(JObject criteria,ILogger log);
        public Task UpdateEMPI(string eventType, JObject fhirresource,ILogger log);
    }
}
