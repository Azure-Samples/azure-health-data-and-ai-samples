using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
	public class ServiceBaseUrlBundleGeneratorService : IServiceBaseUrlBundleGeneratorService
	{
		private ILogger _logger;
		private IClientConfigService _clientConfigService;

		public ServiceBaseUrlBundleGeneratorService(ILogger<ServiceBaseUrlBundleGeneratorService> logger, IClientConfigService clientConfigService)
		{
			_logger = logger;
			_clientConfigService = clientConfigService;
		}

		public async Task<Bundle> CreateBundle()
		{
			_logger.LogInformation("CreateBundle() Starts");

			Bundle bundle;
			try
			{
				// Create a Endpoint from the keyvault values
				var status = await _clientConfigService.FetchSecretAsync("status");
				var connectionType = await _clientConfigService.FetchSecretAsync("connectionType");
				var address = await _clientConfigService.FetchSecretAsync("address");

				var endpointResource = new EndpointResource
				{
					ResourceType = ResourceType.Endpoint.ToString(),
					Id = Guid.NewGuid().ToString(),
					Status = status.Value.Value,
					ConnectionType = new ConnectionType
					{
						System = connectionType.Value.Value,
						Code = "hl7-fhir-rest"
					},
					Name = "Health Intersections CarePlan Hub",
					PayloadType = new List<PayloadType>
					{
						new PayloadType
						{
							Coding = new List<Coding>
							{
								new Coding
								{
									System = "http://hl7.org/fhir/resource-types",
									Code = "CarePlan"
								}
							}
						}
					},
					Address = address.Value.Value
				};

				// Create a Organization from the keyvault values
				var active = await _clientConfigService.FetchSecretAsync("active");
				var name = await _clientConfigService.FetchSecretAsync("name");
				var location = await _clientConfigService.FetchSecretAsync("location");
				var identifier = await _clientConfigService.FetchSecretAsync("identifier");

				var organizationResource = new OrganizationResource
				{
					ResourceType = ResourceType.Organization.ToString(),
					Id = Guid.NewGuid().ToString(),
					Identifier = new List<Identifier>
					{
						new Identifier { System = identifier.Value.Value, Value = "1407071210" }
					},
					Active = bool.Parse(active.Value.Value),
					Name = name.Value.Value,
					Address = new List<Address>
					{
						new Address
						{
							Line = new List<string> { "3300 Washtenaw Avenue, Suite 227" },
							City = "Ann Arbor",
							State = "MI",
							PostalCode = "48104",
							Country = location.Value.Value
						}
					},
					Endpoint = new List<EndpointReference>
					{
						new EndpointReference { Reference = $"Endpoint/{endpointResource.Id}" }
					}
				};

				bundle = new Bundle
				{
					ResourceType = ResourceType.Bundle.ToString(),
					Id = Guid.NewGuid().ToString(),
					Type = "collection",
					Entry = new List<Entry>
					{
						new Entry
						{
							FullUrl = $"{endpointResource.Address}/{organizationResource.ResourceType}/{organizationResource.Id}",
							Resource = organizationResource
						},
						new Entry
						{
							FullUrl = $"{endpointResource.Address}/{endpointResource.ResourceType}/{endpointResource.Id}",
							Resource = endpointResource
						}
					}
				};
			}
			catch (Exception ex)
			{
				_logger.LogError("Unexpected error encountered while creating service base url list bundle {error}", ex);
				throw new InvalidDataException($"Unexpected error encountered while creating service base url list bundle {ex}");
			}

			return bundle;

		}
	}
}
