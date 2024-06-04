using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EMPIShim
{
  
    public class MatchResult
    {
        public int CandidateTotal { get;set; }
        public int CandidateCount { get; set; }
        public int MatchedResultsFound { get; set; }
        public IEnumerable<MatchCandidate> Result { get; set; }
    }
}
