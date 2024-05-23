using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public interface IBundleGeneratorService
    {
        public Task<ServiceBaseObject.Bundle> CreateBundle();
    }
}
