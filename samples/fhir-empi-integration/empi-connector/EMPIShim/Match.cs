using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Web.Http;

namespace EMPIShim
{
    public static class Match
    {
        private static IEMPIProvider _provider = Utils.EMPIProviderGetInstance(Utils.GetEnvironmentVariable("EMPIProvider", "EMPIShim.NoOpEMPIProvider"));
        [FunctionName("Match")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            JObject o = null;
            string fhirsysid = Utils.GetEnvironmentVariable("EMPIFHIRSystemId");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            try
            {
                o = JObject.Parse(requestBody);
            }
            catch (Exception e)
            {
                return new BadRequestErrorMessageResult("Invalid or Empty JSON Data in Request Body: " + e.Message);
            }
           
            MatchResult result = await _provider.RunMatch(o, log);
            //Results Process
            JObject retval = new JObject();
            retval["resourceType"] = "Bundle";
            retval["id"] = Guid.NewGuid();
            retval["meta"] = new JObject();
            retval["meta"]["lastUpdated"]=DateTime.UtcNow;
            retval["type"] = "searchset";
            retval["total"] = 0;
            JArray patients = new JArray();
            if (result.CandidateTotal > 0)
            {
                int c = 0;
                foreach (MatchCandidate mc in result.Result)
                {
                   
                    var sysid = mc.SystemIdentifiers.FirstOrDefault(si => si.System.Equals(fhirsysid));
                    if (sysid != null)
                    {
                        var pat = await FHIRUtils.CallFHIRServer($"Patient/{sysid.Id}", "", System.Net.Http.HttpMethod.Get, log);
                        if (pat.Success)
                        {
                            JObject pe = new JObject();
                            pe["fullUrl"] = $"{Utils.GetEnvironmentVariable("FS_URL")}/Patient/{sysid.Id}";
                            pe["resource"] = JObject.Parse(pat.Content);
                            pe["search"] = searchEntry(mc);
                            patients.Add(pe);
                            retval["total"] = ++c;
                        }

                    }
                }
              
            }
            retval["entry"] = patients;
            return new JsonResult(retval);
        }
      
        public static JObject searchEntry(MatchCandidate mc)
        {
            JObject retval = new JObject();
            JArray extarr = new JArray();
            JObject o1 = new JObject();
            string certainty = "";
            o1["url"] = "http://hl7.org/fhir/StructureDefinition/match-grade";
            if (mc.Certainty==Certainty.certainlynot) certainty = "certainly-not";
            else if (mc.Certainty==Certainty.possible) certainty = "possible";
            else if (mc.Certainty==Certainty.probable) certainty = "probable";
            else if (mc.Certainty==Certainty.certain) certainty = "certain";
            o1["valueCode"] = certainty;
            extarr.Add(o1);
            retval["extension"] = extarr;
            retval["mode"] = "search";
            retval["score"] = mc.Score;
            return retval;
        }
    }
   
}
