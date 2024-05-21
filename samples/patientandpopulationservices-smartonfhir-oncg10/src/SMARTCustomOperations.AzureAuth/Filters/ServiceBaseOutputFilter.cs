using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class ServiceBaseOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id;
        private readonly IClientConfigService _clientConfigService;

        public ServiceBaseOutputFilter(ILogger<ServiceBaseOutputFilter> logger,
            AzureAuthOperationsConfig configuration, IClientConfigService clientConfigService) 
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
            _clientConfigService = clientConfigService;
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(ServiceBaseOutputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;


        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            if(!context.Request.RequestUri!.LocalPath.Contains("service-base", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            string responseBody = "{\"resourceType\":\"Bundle\",\"id\":\"de159614104bd3b6e8d88d740e043f64\",\"type\":\"collection\",\"entry\":[{\"fullUrl\":\"https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com/Organization/eaccf0de-c238-4140-9cb6-c9c59f0e5fb3\",\"resource\":{\"resourceType\":\"Organization\",\"id\":\"eaccf0de-c238-4140-9cb6-c9c59f0e5fb3\",\"identifier\":[{\"system\":\"http://hl7.org/fhir/sid/us-npi\",\"value\":\"1407071210\"}],\"name\":\"Health Level Seven International\",\"address\":[{\"line\":[\"3300 Washtenaw Avenue, Suite 227\"],\"city\":\"Ann Arbor\",\"state\":\"MI\",\"postalCode\":\"48104\",\"country\":\"USA\"}],\"endpoint\":[{\"reference\":\"Endpoint/a1aa7db0-daf6-42d7-bba5-583b6869ccd2\"}]}},{\"fullUrl\":\"https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com/Endpoint/a1aa7db0-daf6-42d7-bba5-583b6869ccd2\",\"resource\":{\"resourceType\":\"Endpoint\",\"id\":\"a1aa7db0-daf6-42d7-bba5-583b6869ccd2\",\"status\":\"active\",\"connectionType\":{\"system\":\"http://terminology.hl7.org/CodeSystem/endpoint-connection-type\",\"code\":\"hl7-fhir-rest\"},\"name\":\"Health Intersections CarePlan Hub\",\"payloadType\":[{\"coding\":[{\"system\":\"http://hl7.org/fhir/resource-types\",\"code\":\"CarePlan\"}]}],\"address\":\"https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com\"}}]}";


            ServiceBaseObject.Endpoint endpoint = new ServiceBaseObject.Endpoint
            {
                Id = "a1aa7db0-daf6-42d7-bba5-583b6869ccd2",
                Status = "active",
                ConnectionType = new ServiceBaseObject.Endpoint.ConnectionTypeClass
                {
                    System = "http://terminology.hl7.org/CodeSystem/endpoint-connection-type",
                    Code = "hl7-fhir-rest"
                },
                Name = "Health Intersections CarePlan Hub",
                PayloadType = new List<ServiceBaseObject.Endpoint.PayloadTypeClass>
                {
                    new ServiceBaseObject.Endpoint.PayloadTypeClass
                    {
                        Coding = new List<ServiceBaseObject.Endpoint.PayloadTypeClass.CodingClass>
                        {
                            new ServiceBaseObject.Endpoint.PayloadTypeClass.CodingClass
                            {
                                System = "http://hl7.org/fhir/resource-types",
                                Code = "CarePlan"
                            }
                        }
                    }
                },
                Address = "https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com"
            };

            ServiceBaseObject.Organization organization = new ServiceBaseObject.Organization
            {
                Id = "eaccf0de-c238-4140-9cb6-c9c59f0e5fb3",
                Identifier = new List<ServiceBaseObject.Organization.IdentifierClass>
                {
                    new ServiceBaseObject.Organization.IdentifierClass
                    {
                        System = "http://hl7.org/fhir/sid/us-npi",
                        Value = "1407071210"
                    }
                },
                Name = "Health Level Seven International",
                Address = new List<ServiceBaseObject.Organization.AddressClass>
                {
                    new ServiceBaseObject.Organization.AddressClass
                    {
                        Line = new List<string> { "3300 Washtenaw Avenue, Suite 227" },
                        City = "Ann Arbor",
                        State = "MI",
                        PostalCode = "48104",
                        Country = "USA"
                    }
                },
                Endpoint = new List<ServiceBaseObject.Organization.EndpointReference>
                {
                    new ServiceBaseObject.Organization.EndpointReference
                    {
                        Reference = "Endpoint/a1aa7db0-daf6-42d7-bba5-583b6869ccd2"
                    }
                }
            };


            var bundle = new ServiceBaseObject.Bundle
            {
                ResourceType = "Bundle",
                Id = "de159614104bd3b6e8d88d740e043f64",
                Type = "collection",
                Entry = new List<ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>>
                {
                    new ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>
                    {
                        FullUrl = "https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com/Organization/eaccf0de-c238-4140-9cb6-c9c59f0e5fb3",
                        Resource = organization
                    },
                    new ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>
                    {
                        FullUrl = "https://vrakoncandb2chealth-fhirdata.fhir.azurehealthcareapis.com/Endpoint/a1aa7db0-daf6-42d7-bba5-583b6869ccd2",
                        Resource = endpoint
                    }
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            string jsonString = JsonSerializer.Serialize(bundle, options);

            // string org = JsonSerializer.Serialize(organization, options);
            context.ContentString = jsonString;
            context.StatusCode = System.Net.HttpStatusCode.OK;

            await Task.CompletedTask;

            return context;
        }
    }
}
