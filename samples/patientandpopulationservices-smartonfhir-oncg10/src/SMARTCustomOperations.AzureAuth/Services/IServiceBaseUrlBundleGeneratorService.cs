using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public interface IServiceBaseUrlBundleGeneratorService
    {
        public Task<Bundle> CreateBundle();
    }
}
