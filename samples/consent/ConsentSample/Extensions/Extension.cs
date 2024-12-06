using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AzureHealth.DataServices.Json;

namespace ConsentSample.Extensions
{
    public static class Extension
    {
        public static string FHIRResourceId(this JToken token)
        {
            if (!token.IsNullOrEmpty() && !token["id"].IsNullOrEmpty()) return (string)token["id"];
            return "";
        }
        public static string FHIRResourceType(this JToken token)
        {
            if (!token.IsNullOrEmpty() && !token["resourceType"].IsNullOrEmpty()) return (string)token["resourceType"];
            return "";
        }
        public static string FHIRVersionId(this JToken token)
        {
            if (!token.IsNullOrEmpty() && !token["meta"].IsNullOrEmpty())
            {
                return (string)token["meta"]?["versionId"];
            }
            return "";
        }
        public static string FHIRLastUpdated(this JToken token)
        {
            if (!token.IsNullOrEmpty() && !token["meta"].IsNullOrEmpty() && !token["meta"]["lastUpdated"].IsNullOrEmpty())
            {
                return JsonConvert.SerializeObject(token["meta"]?["lastUpdated"]).Replace("\"", "");
            }
            return "";
        }
        public static string FHIRReferenceId(this JToken token)
        {
            if (!token.IsNullOrEmpty() && !token["resourceType"].IsNullOrEmpty() && !token["id"].IsNullOrEmpty())
            {
                return (string)token["resourceType"] + "/" + (string)token["id"];
            }
            return "";
        }
    }
}
