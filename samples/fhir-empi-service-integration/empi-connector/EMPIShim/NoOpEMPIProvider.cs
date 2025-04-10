using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMPIShim
{
    internal class NoOpEMPIProvider : IEMPIProvider
    {
        public Task<MatchResult> RunMatch(JObject criteria, ILogger log)
        {
            throw new NotImplementedException();
        }

        public Task UpdateEMPI(string eventType, JObject fhirresource, ILogger log)
        {
            throw new NotImplementedException();
        }
    }
}
