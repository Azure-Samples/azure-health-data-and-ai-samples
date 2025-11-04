using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth.Factories
{
    public interface ITokenIntrospectionServiceFactory
    {
        public ITokenIntrospectionService GetService(string issuer);
    }
}
