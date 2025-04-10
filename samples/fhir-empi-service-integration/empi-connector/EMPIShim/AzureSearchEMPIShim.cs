using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Azure.Search.Documents;
using Azure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using static System.Formats.Asn1.AsnWriter;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Net.Http;

namespace EMPIShim
{
	internal class AzureSearchEMPIShim : IEMPIProvider
	{
		private static string fsurl = Utils.GetEnvironmentVariable("FS-URL");
		private string searchIndex = Environment.GetEnvironmentVariable("SEARCH_INDEX");
		private static HttpClient _empiclient = new HttpClient();
		public async Task<MatchResult> RunMatch(JObject criteria, ILogger log)
		{


			StringBuilder searchString = new StringBuilder();
			SearchOptions searchOptions = new SearchOptions();
			int limit = 1000;
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
							JArray ids = (JArray)res["identifier"];
							if (ids != null)
							{
								foreach (JToken id in ids)
								{
									string system = (string)id["system"];
									string idval = (string)id["value"];
									//if (!string.IsNullOrEmpty(system)) searchString.Append(system + " ");
									if (!string.IsNullOrEmpty(idval)) searchString.Append("identifier/value:" + idval);
								}
							}
							//Add Gender
							string gender = (String)res["gender"];
							if (!string.IsNullOrEmpty(gender))
							{
								if (searchString.Length > 0) searchString.Append(" OR ");
								searchString.Append("gender:" + gender);
							}
							//Add Birthdate
							string bd = (String)res["birthDate"];
							if (!string.IsNullOrEmpty(bd))
							{
								if (searchString.Length > 0) searchString.Append(" OR ");
								searchString.Append("birthDate:" + bd);
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
										if (searchString.Length > 0) searchString.Append(" OR ");
										searchString.Append("name/family:" + family + "~");
									}
									JArray givennames = (JArray)n["given"];
									if (givennames != null)
									{

										foreach (JToken givenname in givennames)
										{
											if (searchString.Length > 0) searchString.Append(" OR ");
											searchString.Append("name/given:" + givenname + "~");
										}

									}

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



			Uri endpoint = new Uri(Environment.GetEnvironmentVariable("SEARCH_ENDPOINT"));
			AzureKeyCredential credential = new AzureKeyCredential(
				Environment.GetEnvironmentVariable("SEARCH_API_KEY"));

			// Create a new SearchIndexClient
			SearchIndexClient indexClient = new SearchIndexClient(endpoint, credential);
			SearchClient searchClient = indexClient.GetSearchClient(searchIndex);
			searchOptions.IncludeTotalCount = true;
			searchOptions.QueryType = SearchQueryType.Full;
			searchOptions.SearchMode = SearchMode.Any;
			//searchOptions.SearchFields.Add("gender");
			//searchOptions.SearchFields.Add("birthDate");
			//searchOptions.SearchFields.Add("identifier/value");
			//searchOptions.SearchFields.Add("name/family");
			//searchOptions.SearchFields.Add("name/given");
			log.LogInformation("Searching for: " + searchString.ToString());
			SearchResults<SearchDocument> results = await searchClient.SearchAsync<SearchDocument>(searchString.ToString(), searchOptions);
			MatchResult retVal = new MatchResult();
			var candidates = new List<MatchCandidate>();
			if (results.TotalCount > 0)
			{
				int c = 0;
				foreach (SearchResult<SearchDocument> result in results.GetResults())
				{
					if (c > limit) break;
					retVal.CandidateTotal = c;
					SearchDocument document = result.Document;
					MatchCandidate mc = new MatchCandidate();
					string id = (string)document["id"];
					mc.EnterpriseId = id;
					mc.ScoreExplantaion = "http://hl7.org/fhir/StructureDefinition/match-grade";
					double s = Math.Round(result.Score.Value / 10, 2);
					mc.Score = s;
					if (s <= .5) mc.Certainty = Certainty.certainlynot;
					else if (s > .5 && s <= .7) mc.Certainty = Certainty.possible;
					else if (s > .7 && s < .9) mc.Certainty = Certainty.probable;
					else mc.Certainty = Certainty.certain;
					var identifiers = new List<SystemIdentifier>();
					identifiers.Add(new SystemIdentifier() { Id = id, System = fsurl });
					mc.SystemIdentifiers = identifiers;
					if (ocm)
					{
						if (s >= .9)
						{
							candidates.Add(mc);
							retVal.CandidateTotal = 1;
						}
						else
						{
							retVal.CandidateTotal = 0;
						}
						break;
					}
					else
					{
						candidates.Add(mc);

					}
					c++;

				}


			}
			retVal.Result = candidates;
			return retVal;
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
						o["value"] = new JArray();
						var action = (eventType.Equals("Microsoft.HealthcareApis.FhirResourceCreated") ? "upload" : "merge");
						fhirresource["@search.action"] = action;
						fhirresource.Property("meta").Remove();
						if (fhirresource["name"] != null)
						{
							JArray a = (JArray)fhirresource["name"];
							foreach (JToken t in a)
							{
								JObject jt = (JObject)t;
								if (jt.Property("text") != null)
								{
									jt.Property("text").Remove();
								}
							}

						}
						if (fhirresource["address"] != null)
						{
							JArray a = (JArray)fhirresource["address"];
							foreach (JToken t in a)
							{
								JObject jt = (JObject)t;
								if (jt.Property("use") != null)
								{
									jt.Property("use").Remove();
								}
							}

						}
								((JArray)o["value"]).Add(fhirresource);
						log.LogInformation(o.ToString(Newtonsoft.Json.Formatting.Indented));
						var url = Environment.GetEnvironmentVariable("SEARCH_ENDPOINT") + $"/indexes/{searchIndex}/docs/index?api-version=2023-11-01";
						var empiRequest = new HttpRequestMessage(HttpMethod.Post, url);
						empiRequest.Headers.Add("api-key", Utils.GetEnvironmentVariable("SEARCH_API_KEY"));
						empiRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
						empiRequest.Content = new StringContent(o.ToString(), Encoding.UTF8, "application/json");
						var resp = await _empiclient.SendAsync(empiRequest);
						var rs = await resp.Content.ReadAsStringAsync();
						log.LogInformation(rs);
					}
					break;
				case "Microsoft.HealthcareApis.FhirResourceDeleted":
					string fhirid = fhirresource["id"].ToString();
					var o1 = new JObject();
					o1["value"] = new JArray();
					var deleteObj = new JObject();
					deleteObj["@search.action"] = "delete";
					deleteObj["id"] = fhirid;
					((JArray)o1["value"]).Add(deleteObj);
					var url1 = Environment.GetEnvironmentVariable("SEARCH_ENDPOINT") + $"/indexes/{searchIndex}/docs/index?api-version=2023-11-01";
					var empiRequest1 = new HttpRequestMessage(HttpMethod.Post, url1);
					empiRequest1.Headers.Add("api-key", Utils.GetEnvironmentVariable("SEARCH_API_KEY"));
					empiRequest1.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
					empiRequest1.Content = new StringContent(o1.ToString(), Encoding.UTF8, "application/json");
					var resp1 = await _empiclient.SendAsync(empiRequest1);
					var rs1 = await resp1.Content.ReadAsStringAsync();
					log.LogInformation(rs1);
					break;
			}
		}
	}
}
