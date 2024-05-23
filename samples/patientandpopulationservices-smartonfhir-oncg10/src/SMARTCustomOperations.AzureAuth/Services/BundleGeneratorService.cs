using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public class BundleGeneratorService : IBundleGeneratorService
    {
        private ILogger _logger;
        private IClientConfigService _clientConfigService;

        public BundleGeneratorService(ILogger<BundleGeneratorService> logger, IClientConfigService clientConfigService)
        {
            _logger = logger;
            _clientConfigService = clientConfigService;
        }

        public async Task<ServiceBaseObject.Bundle> CreateBundle()
        {
            List<string> endpointKeys = new List<string>() { "status", "connectionType", "address" };
            var endpoint = await _clientConfigService.FetchEndpointConfiguration(endpointKeys);

            List<string> orgKeys = new List<string>() { "active", "name", "location", "identifier" };
            var organization = await _clientConfigService.FetchOrganizationConfiguration(orgKeys, endpoint.Id);

            var bundle = new ServiceBaseObject.Bundle
            {
                Entry = new List<ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>>
                {
                    new ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>
                    {
                        FullUrl = $"{endpoint.Address}/{organization.ResourceType}/{organization.Id}",
                        Resource = organization
                    },
                    new ServiceBaseObject.Entry<ServiceBaseObject.ResourceBase>
                    {
                        FullUrl = $"{endpoint.Address}/{endpoint.ResourceType}/{endpoint.Id}",
                        Resource = endpoint
                    }
                }
            };

            return bundle;
            
        }
    }
}
