using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMPIShim
{
    public enum Certainty
    {
        certainlynot,
        possible,
        probable,
        certain
    }
   
    public class MatchCandidate
    {
        public string EnterpriseId { get; set; }
        public double Score { get; set; }
        public Certainty Certainty { get; set; }
        public string ScoreExplantaion { get; set; }
        public IEnumerable<SystemIdentifier> SystemIdentifiers { get; set; }
    }
    public class SystemIdentifier
    {
        public string Id { get; set; }
        public string System { get; set; }
        public string Status { get; set; }
    }
}
