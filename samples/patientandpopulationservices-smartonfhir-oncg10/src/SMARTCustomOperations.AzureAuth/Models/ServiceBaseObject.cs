using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class ServiceBaseObject
    {
        public class Bundle
        {
            [JsonPropertyName("resourceType")]
            public string? ResourceType { get; set; } = "Bundle";
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            [JsonPropertyName("type")]
            public string? Type { get; set; } = "collection";
            [JsonPropertyName("entry")]
            public List<Entry<ResourceBase>>? Entry { get; set; }
        }

        public class Entry<T> where T : ResourceBase
        {
            [JsonPropertyName("fullUrl")]
            public string? FullUrl { get; set; }
            [JsonPropertyName("resource")]
            public T? Resource { get; set; }
        }

        public abstract class ResourceBase
        {
            [JsonPropertyName("resourceType")]
            public string? ResourceType { get; set; }
            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }

        public class Organization : ResourceBase
        {
            public Organization()
            {
                ResourceType = "Organization";
            }
            [JsonPropertyName("identifier")]
            public List<IdentifierClass>? Identifier { get; set; }
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("address")]
            public List<AddressClass>? Address { get; set; }
            [JsonPropertyName("endpoint")]
            public List<EndpointReference>? Endpoint { get; set; }

            public class IdentifierClass
            {
                [JsonPropertyName("system")]
                public string? System { get; set; }
                [JsonPropertyName("value")]
                public string? Value { get; set; }
            }

            public class AddressClass
            {
                [JsonPropertyName("line")]
                public List<string>? Line { get; set; }
                [JsonPropertyName("city")]
                public string? City { get; set; }
                [JsonPropertyName("state")]
                public string? State { get; set; }
                [JsonPropertyName("postalCode")]
                public string? PostalCode { get; set; }
                [JsonPropertyName("country")]
                public string? Country { get; set; }
            }

            public class EndpointReference
            {
                [JsonPropertyName("reference")]
                public string? Reference { get; set; }
            }
        }

        public class Endpoint : ResourceBase
        {
            public Endpoint()
            {
                ResourceType = "Endpoint";
            }
            [JsonPropertyName("status")]
            public string? Status { get; set; }
            [JsonPropertyName("connectionType")]
            public ConnectionTypeClass? ConnectionType { get; set; }
            [JsonPropertyName("name")]
            public string? Name { get; set; }
            [JsonPropertyName("payloadType")]
            public List<PayloadTypeClass>? PayloadType { get; set; }
            [JsonPropertyName("address")]
            public string? Address { get; set; }

            public class ConnectionTypeClass
            {
                [JsonPropertyName("system")]
                public string? System { get; set; }
                [JsonPropertyName("code")]
                public string? Code { get; set; }
            }

            public class PayloadTypeClass
            {
                [JsonPropertyName("coding")]
                public List<CodingClass>? Coding { get; set; }

                public class CodingClass
                {
                    [JsonPropertyName("system")]
                    public string? System { get; set; } = "http://hl7.org/fhir/resource-types";
                    [JsonPropertyName("code")]
                    public string? Code { get; set; } = "CarePlan";
                }
            }
        }
    }
}
