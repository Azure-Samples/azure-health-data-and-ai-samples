using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.EventHubs;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Azure.Search.Documents.Models;
using System.Net.Http;
using Microsoft.Azure.Amqp.Framing;
using Polly;
using System.Net.Sockets;
using System.Reflection;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using static System.Collections.Specialized.BitVector32;
using System.Runtime.CompilerServices;

namespace EMPIShim
{
    public static class EMPIUpdates
    {
        private static HttpClient _empiclient = new HttpClient();
        private static IEMPIProvider _provider = Utils.EMPIProviderGetInstance(Utils.GetEnvironmentVariable("EMPIProvider", "EMPIShim.NoOpEMPIProvider"));

        [FunctionName("EMPIUpdates")]
        public static async Task Run([EventHubTrigger("empieventhub", Connection = "evconnect")] EventData[] events, ILogger log)
        {
            log.LogInformation($"Empi Updates triggered");
            var exceptions = new List<Exception>();

            foreach (EventData eventData in events)
            {
                try
                {
                    JArray arr = JArray.Parse(eventData.EventBody.ToString());
                    StringBuilder sb = new StringBuilder();
                    foreach (JToken token in arr)
                    {
                        var fhirrt = token["data"]["resourceType"].ToString();
                        var fhirid = token["data"]["resourceFhirId"].ToString();
                        var eventversionid = token["data"]["resourceVersionId"].ToString();
                        var eventtype = token["eventType"].ToString();
                        JObject resource = null;
                        if (!eventtype.Equals("Microsoft.HealthcareApis.FhirResourceDeleted"))
                        {
                            var resp = await FHIRUtils.CallFHIRServer($"{fhirrt}/{fhirid}", "", System.Net.Http.HttpMethod.Get, log);
                            if (resp.Success && !string.IsNullOrEmpty(resp.Content))
                            {
                                resource = JObject.Parse(resp.Content);
                                if (resource["meta"] != null && resource["meta"]["versionId"] != null)
                                {
                                    var resourceversion = resource["meta"]["versionId"].ToString();
                                    if (eventversionid.Equals(resourceversion))
                                    {
                                        await _provider.UpdateEMPI(eventtype, resource, log);
                                    }
                                }
                            }
                        } else
                        {
                            resource = new JObject();
                            resource["id"] = fhirid;
                            resource["resourceType"] = fhirrt;
                            await _provider.UpdateEMPI(eventtype, resource, log);
                        }
                    }
                    log.LogInformation(arr.ToString(Newtonsoft.Json.Formatting.Indented));
                    await Task.Yield();
                }
                catch (Exception e)
                {
                    // We need to keep processing the rest of the batch - capture this exception and continue.
                    // Also, consider capturing details of the message that failed processing so it can be processed again later.
                    exceptions.Add(e);
                }
            }

            // Once processing of the batch is complete, if any messages in the batch failed processing throw an exception so that there is a record of the failure.

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }
    }
}
