using Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Microsoft.Azure.Amqp.Serialization.SerializableType;
using static System.Formats.Asn1.AsnWriter;

namespace EMPIShim
{
    public class NextGateEMPIProvider : IEMPIProvider
    {
        private static HttpClient _empiclient = new HttpClient();
        private async Task initAuthClient(ILogger log)
        {
            var url = Environment.GetEnvironmentVariable("NEXTGATE_URL") + "/ws/auth/auth/authenticate";
            var dict = new Dictionary<string, string>();
            dict.Add("username", Utils.GetEnvironmentVariable("NEXTGATE_USERNAME"));
            dict.Add("password", Utils.GetEnvironmentVariable("NEXTGATE_PASSWORD"));
            using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(dict) };
            using var resp = await _empiclient.SendAsync(req);
            var rs = await resp.Content.ReadAsStringAsync();
            if (resp.IsSuccessStatusCode)
            {
                CookieContainer cookies = new CookieContainer();
                Uri myUri = new Uri(Environment.GetEnvironmentVariable("NEXTGATE_URL"));
                foreach (var cookieHeader in resp.Headers.GetValues("Set-Cookie"))
                    cookies.SetCookies(myUri, cookieHeader);
                string cookieValue = cookies.GetCookies(myUri).FirstOrDefault(c => c.Name == "XSRF-TOKEN")?.Value;
                _empiclient.DefaultRequestHeaders.Clear();
                _empiclient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _empiclient.DefaultRequestHeaders.TryAddWithoutValidation("X-XSRF-TOKEN", cookieValue);
                _empiclient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", Environment.GetEnvironmentVariable("NEXTGATE_URL"));
                log.LogInformation($"Authenticated to NextGate Token: {cookieValue}");
            } else
            {
                log.LogError($"Unable to Authenticate to NextGate:{rs}");
            }
          
            

        }
        public async Task<MatchResult> RunMatch(JObject criteria, ILogger log)
        {
            JObject sc = new JObject();
            int limit = 0;
            bool ocm = false;
            JArray parms = (JArray)criteria["parameter"];
            foreach (JToken t in parms)
            {
                
                if (t["name"] != null && t["name"].ToString().Equals("resource"))
                {
                    JObject res = (JObject)t["resource"];
                    if (res != null)
                    {
                        if (res["resourceType"].ToString().Equals("Patient"))
                        {
                            //Add Identifiers
                            /*JArray ids = (JArray)res["identifier"];
                            if (ids != null)
                            {
                                foreach (JToken id in ids)
                                {
                                    string system = (string)id["system"];
                                    string idval = (string)id["value"];
                                    //if (!string.IsNullOrEmpty(system)) searchString.Append(system + " ");
                                    if (!string.IsNullOrEmpty(idval)) searchString.Append("identifier/value:" + idval);
                                }
                            }*/
                            //Add Gender
                            string gender = (String)res["gender"];
                            if (!string.IsNullOrEmpty(gender))
                            {
                                gender = gender.Substring(0, 1).ToUpper();
                                sc["Gender"] = gender;
                            }
                            //Add Birthdate
                            string bd = (String)res["birthDate"];
                            if (!string.IsNullOrEmpty(bd))
                            {
                                sc["DOB"] = bd +"T00:00:00";
                            }
                            //Add Names
                            JArray names = (JArray)res["name"];
                            if (names != null)
                            {
                                foreach (JToken n in names)
                                {
                                    string family = (String)n["family"];
                                    if (!string.IsNullOrEmpty(family))
                                    {
                                        sc["LastName"]=family;
                                    }
                                    JArray givennames = (JArray)n["given"];
                                    if (givennames != null)
                                    {

                                        foreach (JToken givenname in givennames)
                                        {
                                            sc["FirstName"]=givenname;
                                            break;
                                        }

                                    }
                                    break;
                                }
                            }

                        }
                    }
                }
                else if (t["name"] != null && t["name"].ToString().Equals("count"))
                {
                    string s_cnt = (string)t["valueInteger"];
                    if (!string.IsNullOrEmpty(s_cnt))
                    {
                        limit = int.Parse(s_cnt);
                    }
                }
                else if (t["name"] != null && t["name"].ToString().Equals("onlyCertainMatches"))
                {
                    string s_cm = (string)t["valueBoolean"];
                    if (!string.IsNullOrEmpty(s_cm))
                    {
                        ocm = bool.Parse(s_cm);
                    }
                }
            }
            var url = Environment.GetEnvironmentVariable("NEXTGATE_URL") + $"/ws/mm/PersonRS/search?minScore=1&count={limit}";
            if (!_empiclient.DefaultRequestHeaders.TryGetValues("X-XSRF-TOKEN",out var fred)) await initAuthClient(log);
            var resp = await _empiclient.PostAsync(url,new StringContent(sc.ToString(), Encoding.UTF8, "application/json"));
            if (resp.StatusCode==HttpStatusCode.Unauthorized || resp.StatusCode==HttpStatusCode.Forbidden)
            {
                await initAuthClient(log);
                resp = await _empiclient.PostAsync(url, new StringContent(sc.ToString(), Encoding.UTF8, "application/json"));
            }
            var rs = await resp.Content.ReadAsStringAsync();
            JObject content = JObject.Parse(rs);
            JArray results = (JArray)content["results"];
            MatchResult mr = new MatchResult();
            mr.CandidateTotal = (int)content["candidatesFound"];
            List<MatchCandidate> candidates = new List<MatchCandidate>();
            foreach(JToken t in results)
            {
                MatchCandidate mc = new MatchCandidate();
                mc.ScoreExplantaion = t["weightExplanation"].ToString();
                mc.EnterpriseId = t["euid"].ToString();
                mc.Score = (double)t["weight"];
                bool pfp = (bool)t["potentialFalsePositive"];
                if (mc.Score < 5) mc.Certainty = Certainty.certainlynot;
                else if (mc.Score >= 5 && mc.Score < 8) mc.Certainty = Certainty.possible;
                else if (mc.Score >= 8 && mc.Score < 10) mc.Certainty = Certainty.probable;
                else mc.Certainty = Certainty.certain;
                var urlp = Environment.GetEnvironmentVariable("NEXTGATE_URL") + $"/ws/mm/PersonRS/enterpriserecords/{mc.EnterpriseId}?includeMetadata=true";
                using var resp1 = await _empiclient.GetAsync(urlp);
                var rs1 = await resp1.Content.ReadAsStringAsync();
                JObject content1 = JObject.Parse(rs1);
                JArray records = (JArray)content1["systemRecords"];
                var sysids = new List<SystemIdentifier>();
                foreach(JToken u in records)
                {
                    sysids.Add(new SystemIdentifier() { Id = u["lid"].ToString(),System = u["system"].ToString(),Status = u["status"].ToString() });
                }
                mc.SystemIdentifiers = sysids;
                candidates.Add(mc);
            }
            mr.Result = candidates;
            return mr;
        }

        public async Task UpdateEMPI(string eventType, JObject fhirresource, ILogger log)
        {
            switch (eventType)
            {
                case "Microsoft.HealthcareApis.FhirResourceCreated":
                case "Microsoft.HealthcareApis.FhirResourceUpdated":
                    if (fhirresource != null)
                    {

                        JObject o = new JObject();
                        o["Person"] = new JObject();
                        //Addresses
                        JArray addressarr = (JArray)fhirresource["address"];
                        if (addressarr != null)
                        {
                            JArray empiaddr = new JArray();
                            int addrid = 0;
                            foreach (JToken addr in addressarr)
                            {
                                string use = (addr["use"] == null ? "unknown" : addr["use"].ToString());
                                string line1 = "";
                                JArray lines = (JArray)addr["line"];
                                if (lines != null)
                                {
                                    line1 = lines[0].ToString();
                                }
                                string city = (addr["city"] != null ? addr["city"].ToString() : "");
                                string state = (addr["state"] != null ? addr["state"].ToString() : string.Empty);
                                string postalcode = (addr["postalCode"] != null ? addr["postalCode"].ToString() : string.Empty);
                                string addrtype = (use.Substring(0, 1).ToUpper());
                                JObject empio = new JObject();
                                empio["AddressId"] = addrid.ToString();
                                empio["AddressLine1"] = line1;
                                empio["AddressType"] = "H";
                                empio["City"] = city;
                                empio["StateCode"] = state;
                                empio["PostalCode"] = postalcode;
                                empiaddr.Add(empio);
                                addrid++;
                                break;
                            }
                            o["Person"]["Address"] = empiaddr;
                        }
                        //Date of Birth
                        if (fhirresource["birthDate"] != null)
                        {
                            o["Person"]["DOB"] = fhirresource["birthDate"].ToString() + "T00:00:00";
                        }
                        //Gender
                        if (fhirresource["gender"] != null)
                        {
                            o["Person"]["Gender"] = fhirresource["gender"].ToString().Substring(0, 1).ToUpper();
                        }
                        //Person LID
                        //o["Person"]["PersonId"] = fhirresource["id"].ToString();
                        //Name
                        JArray names = (JArray)fhirresource["name"];
                        if (names != null && names.Count > 0)
                        {
                            JToken n = names[0];
                            string family = (String)n["family"];
                            if (!string.IsNullOrEmpty(family))
                            {
                                o["Person"]["LastName"] = family;
                                JArray givennames = (JArray)n["given"];
                                if (givennames != null && givennames.Count > 0)
                                {
                                    o["Person"]["FirstName"] = givennames[0].ToString();
                                }
                            }
                        }
                        //Phone
                        JArray telecom = (JArray)fhirresource["telecom"];
                        if (telecom != null)
                        {
                            JArray empiphone = new JArray();
                            int phoneid = 0;
                            foreach (JToken phone in telecom)
                            {
                                if (phone["system"] != null && phone["system"].ToString().ToLower().Equals("phone"))
                                {
                                    string phonenum = (phone["value"] != null ? phone["value"].ToString() : "");
                                    string phoneuse = (phone["use"] == null ? "unknown" : phone["use"].ToString());
                                    string phonetype = (phoneuse.Substring(0, 1).ToUpper());
                                    JObject empphone = new JObject();
                                    empphone["PhoneId"] = phoneid.ToString();
                                    empphone["Phone"] = phonenum;
                                    empphone["PhoneType"] = "H";
                                    empiphone.Add(empphone);
                                }
                                phoneid++;
                                break;
                            }
                            o["Person"]["Phone"] = empiphone;
                        }
                        //Identifiers
                        JArray identifiers = (JArray)fhirresource["identifier"];
                        if (identifiers != null)
                        {
                            //SSN
                            foreach (JToken idd in identifiers)
                            {
                                if (idd["system"] != null && idd["system"].ToString().Equals("http://hl7.org/fhir/sid/us-ssn"))
                                {
                                    string ssn = (idd["value"] != null ? idd["value"].ToString() : "");
                                    o["Person"]["SSN"] = ssn.Replace("-", "");
                                    break;
                                }

                            }

                        }
                        log.LogInformation(o.ToString(Newtonsoft.Json.Formatting.Indented));
                        var url = Environment.GetEnvironmentVariable("NEXTGATE_URL") + $"/ws/mm/PersonRS/systemrecords/{Utils.GetEnvironmentVariable("EMPIFHIRSystemId")}/{fhirresource["id"].ToString()}?match=true";
                        if (!_empiclient.DefaultRequestHeaders.TryGetValues("X-XSRF-TOKEN", out var fred)) await initAuthClient(log);
                        var resp = await _empiclient.PostAsync(url, new StringContent(o.ToString(), Encoding.UTF8, "application/json"));
                        if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                        {
                            await initAuthClient(log);
                            resp = await _empiclient.PostAsync(url, new StringContent(o.ToString(), Encoding.UTF8, "application/json"));
                        }
                        var rs = await resp.Content.ReadAsStringAsync();
                        if (resp.IsSuccessStatusCode)
                        {
                            log.LogInformation($"Sucess: Reply from NextGate EMPI:\r\n{rs}");
                        } else
                        {
                            log.LogError($"Failed to update NextGate:\r\n {rs}");
                            _empiclient.DefaultRequestHeaders.Clear();
                        }
                    }
                    break;
                case "Microsoft.HealthcareApis.FhirResourceDeleted":
                    var urld = Environment.GetEnvironmentVariable("NEXTGATE_URL") + $"/ws/mm/PersonRS/systemrecords/{Utils.GetEnvironmentVariable("EMPIFHIRSystemId")}/{fhirresource["id"].ToString()}/status";
                    if (!_empiclient.DefaultRequestHeaders.TryGetValues("X-XSRF-TOKEN", out var fred1)) await initAuthClient(log);
                    JObject stat = new JObject();
                    stat["value"] = "I";
                    var respd = await _empiclient.PutAsync(urld, new StringContent(stat.ToString(), Encoding.UTF8, "application/json"));
                    if (respd.StatusCode == HttpStatusCode.Unauthorized || respd.StatusCode == HttpStatusCode.Forbidden)
                    {
                        await initAuthClient(log);
                        respd = await _empiclient.PutAsync(urld, new StringContent(stat.ToString(), Encoding.UTF8, "application/json"));
                    }
                    var rs1 = await respd.Content.ReadAsStringAsync();
                    if (respd.IsSuccessStatusCode)
                    {
                        log.LogInformation($"Sucess: Reply from NextGate EMPI:\r\n{rs1}");
                    }
                    else
                    {
                        log.LogError($"Failed to update NextGate:\r\n {rs1}");
                        _empiclient.DefaultRequestHeaders.Clear();
                    }
                    break;
               
            }
          
            
        }
    }
}
