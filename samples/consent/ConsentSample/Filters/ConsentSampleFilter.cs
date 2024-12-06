
using System.Data;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using ConsentSample.Configuration;
using ConsentSample.Extensions;
using ConsentSample.Processors;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Json;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;

namespace ConsentSample.Filters
{
    public class ConsentSampleFilter : IOutputFilter
    {
        private readonly string _id;
        private readonly StatusType _status;
        private readonly TelemetryClient _telemetryClient;
        private readonly ILogger _logger;
        private readonly bool _debug = true;
        private readonly MyServiceConfig _options;
        private readonly IFhirProcessor _fhirProcessor;

        public ConsentSampleFilter(IFhirProcessor fhirProcessor, MyServiceConfig options, TelemetryClient telemetryClient = null, ILogger<ConsentSampleFilter> logger = null)
        {
            _id = Guid.NewGuid().ToString();
            _telemetryClient = telemetryClient;
            _logger = logger;
            _status = StatusType.Normal;
            _options = options;
            _fhirProcessor = fhirProcessor;
        }

        public event EventHandler<FilterErrorEventArgs> OnFilterError;

        public string Id => _id;

        public string Name => "ConsentSampleFilter";

        public StatusType ExecutionStatusType => _status;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            string role = string.Empty;
            string id = string.Empty;
            string resourceId = string.Empty;
            string resourceType = string.Empty;
            ClaimsPrincipal principal = context.Request.GetClaimsPrincipal();
            if (principal != null)
            {
                ClaimsIdentity ci = (ClaimsIdentity)principal.Identity;
                var roletype = ci.Claims.Where(c => c.Type == "role");
                if (roletype.Any())
                {
                    role = roletype.Single().Value;
                }
                var roleid = ci.Claims.Where(c => c.Type == "role-id");
                if (roleid.Any())
                {
                    id = roleid.Single().Value;
                }

            }
            var headers = context.Request.Headers;
            if (context.Request.Method == HttpMethod.Get)
            {

                DateTime start = DateTime.Now;
                try
                {
                    List<string> associations = new List<string>();
                    
                    string url = context.Request.RequestUri.ToString();
                    Uri uri = new Uri(url);
                    string input = uri.AbsolutePath;
                    string[] parts = input.Split('/', StringSplitOptions.RemoveEmptyEntries);

                    // Access the split parts
                    if (parts.Count()>0) resourceType = parts[0];
                    if (parts.Count()>1) resourceId = parts[1];
                    

                    //Load the consent category code from settings
                    string consent_category = _options.consent_category;
                    
                    if (string.IsNullOrEmpty(consent_category))
                    {
                        _logger?.LogWarning("ConsentOptOutFilter: No value for FP-MOD-CONSENT-OPTOUT-CATEGORY in settings...Filter will not execute");
                        return context;
                    }
                    if (context == null || context.Content == null || string.IsNullOrEmpty(context.Content.ToString()))
                    {
                        _logger?.LogInformation("ConsentOptOutFilter: No FHIR Response found in context...Nothing to filter");
                        return context;
                    }

                    JToken result = JToken.Parse(context.ContentString);
                    //Administrator is allowed access
                    if (Utils.inServerAccessRole(context, "A")) return context;
                    
                    if (!role.IsNullOrEmpty() && !id.IsNullOrEmpty()) associations.Add(role + "/" + id);

                    if (role == "Practitioner")
                    {
                        HttpMethod method = HttpMethod.Get;
                        string query = $"/PractitionerRole?practitioner={id}";
                        _logger?.LogInformation("load organization from PractionerRoles.");
                        var response = await _fhirProcessor.CallProcess(method, string.Empty, _options.FhirServerUrl, query, _options.SourceHttpClient);
                        string cnt = response.Content.ReadAsStringAsync().Result;
                        JToken objresponse = JToken.Parse(cnt);
                        if (objresponse.FHIRResourceType().Equals("Bundle"))
                        {
                            JArray entries = (JArray)objresponse["entry"];
                            if (!entries.IsNullOrEmpty())
                            {
                                foreach (JToken tok in entries)
                                {
                                    associations.Add(tok["resource"].FHIRReferenceId());
                                    if (!tok["resource"]["organization"].IsNullOrEmpty() && !tok["resource"]["organization"]["reference"].IsNullOrEmpty())
                                    {
                                        associations.Add((string)tok["resource"]["organization"]["reference"]);
                                    }
                                }
                            }


                        }
                    }

                    if (result.FHIRResourceType().Equals("Bundle"))
                    {
                        JArray entries = (JArray)result["entry"];
                        if (!entries.IsNullOrEmpty())
                        {
                            foreach (JToken tok in entries)
                            {
                                if (await denyAccess(tok["resource"], associations, consent_category))
                                {
                                    JObject denyObj = new JObject();
                                    denyObj["resourceType"] = tok["resource"].FHIRResourceType();
                                    denyObj["id"] = tok["resource"].FHIRResourceId();
                                    denyObj["text"] = new JObject();
                                    denyObj["text"]["status"] = "generated";
                                    denyObj["text"]["div"] = "<div xmlns =\"http://www.w3.org/1999/xhtml\"><p>Patient has withheld access to this resource</p></div>";
                                    tok["resource"] = denyObj;
                                }
                            }

                        }
                    }

                    else if (!result.FHIRResourceType().Equals("OperationOutcome"))
                    {
                        if (await denyAccess(result, associations, consent_category))
                        {
                            
                            context.ContentString = Utils.genOOErrResponse("access-denied", $"The patient has withheld access to this resource:{resourceType + (resourceId == null ? "" : "/" + resourceId)}");
                            context.StatusCode = System.Net.HttpStatusCode.Unauthorized;
                            return context;
                        }
                    }

                    context.ContentString = result.ToString();
                    return context;


                }
                catch (JPathException jpathExp)
                {
                    _logger?.LogError(jpathExp, "{Name}-{Id} filter jpath fault.", Name, Id);
                    context.IsFatal = true;
                    context.StatusCode = HttpStatusCode.BadRequest;
                    FilterErrorEventArgs error = new(Name, Id, true, jpathExp, HttpStatusCode.BadRequest, null);
                    OnFilterError?.Invoke(this, error);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-JPathError", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    return context.SetContextErrorBody(error, _debug);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "{Name}-{Id} filter fault.", Name, Id);
                    context.IsFatal = true;
                    context.StatusCode = HttpStatusCode.InternalServerError;
                    FilterErrorEventArgs error = new(Name, Id, true, ex, HttpStatusCode.InternalServerError, null);
                    OnFilterError?.Invoke(this, error);
                    _telemetryClient?.TrackMetric(new MetricTelemetry($"{Name}-{Id}-Error", TimeSpan.FromTicks(DateTime.Now.Ticks - start.Ticks).TotalMilliseconds));
                    return context.SetContextErrorBody(error, _debug);
                }
            }
            else 
            {
                return context;
            }

        }

        private async Task<bool> denyAccess(JToken resource, List<string> associations, string consentcat)
        {
            string patientId = null;
            List<string> denyactors = new();
            string rt = resource.FHIRResourceType();
            //Check for Patient resource or load patient resource id from subject/patient member
            if (rt.Equals("Patient"))
            {
                patientId = rt + "/" + (string)resource["id"];
            }
            if (patientId == null)
            {
                patientId = (string)resource?["subject"]?["reference"];
                if (string.IsNullOrEmpty(patientId) || !patientId.StartsWith("Patient")) patientId = (string)resource?["patient"]?["reference"];
            }
            //If no patient id present assume not tied to patient do not filter;
            if (string.IsNullOrEmpty(patientId)) return false;
            //Load Cache if needed
            //List<string> denyactors = ((string)cache.StringGet($"{PATIENT_DENY_ACTORS_PREFIX}{patientId}")).DeSerializeList<string>();
            if (denyactors.IsNullOrEmpty())
            {
                //Fetch and Cache Deny access Consent Information
                var pid = patientId.Split("/")[1];
                HttpMethod method = HttpMethod.Get;
                string query = $"/Consent?patient={pid}&category={consentcat}";

                var response = await _fhirProcessor.CallProcess(method, string.Empty, _options.FhirServerUrl, query, _options.SourceHttpClient);
                
                string? cnt = response.Content.ReadAsStringAsync().Result;
                JToken result = JToken.Parse(cnt);

                if (result.FHIRResourceType().Equals("Bundle"))
                {
                    JArray entries = (JArray)result["entry"];
                    if (!entries.IsNullOrEmpty())
                    {
                        denyactors = new List<string>();
                        foreach (JToken tok in entries)
                        {
                            var r = tok["resource"];
                            if (!r["provision"].IsNullOrEmpty())
                            {
                                //Check enforceemnt period
                                if (isEnforced(r["provision"]["period"]))
                                {

                                    string type = (string)r["provision"]["type"];
                                    //Load deny provisions only

                                    if (type != null && type.Equals("deny"))
                                    {
                                        //Load actor references to deny
                                        JArray actors = (JArray)r["provision"]["actor"];
                                        if (!actors.IsNullOrEmpty())
                                        {
                                            foreach (JToken actor in actors)
                                            {
                                                denyactors.Add((string)actor["reference"]["reference"]);
                                            }
                                        }
                                        else
                                        {
                                            //Nobody specified so everybody is denied access this trumps all opt out advise
                                            denyactors.Clear();
                                            denyactors.Add("*");
                                            break;
                                        }
                                    }
                                }
                            }

                        }

                    }
                    else
                    {
                        denyactors = new List<string>();
                    }

                }

            }
            //If there is an empty actor array that means there is no deny provision specified so return false to allow access
            if (denyactors.Count == 0) return false;
            //It there is a wildcard in first entry then everyone is denied return true
            if (denyactors.First().Equals("*")) return true;
            //Check for intersection of denied actors and associations of current user if found access will be denied
            return associations.Select(x => x)
                             .Intersect(denyactors)
                             .Any();


        }

        private bool isEnforced(JToken period)
        {
            //Check Enforecement Period for Provision in Consent
            //no enforcement period specified so it's valid
            if (period.IsNullOrEmpty()) return true;
            //See if we are within the enforcement period
            //If start or end is not specified the lowest/greatest dates are assumed.
            DateTime start = (DateTime)period["start"];
            if (start == null) start = DateTime.MinValue;
            DateTime end = (DateTime)period["end"];
            if (end == null) end = DateTime.MaxValue;
            DateTime now = DateTime.Now;
            if (now >= start && now <= end) return true;
            return false;
        }
    }
}
