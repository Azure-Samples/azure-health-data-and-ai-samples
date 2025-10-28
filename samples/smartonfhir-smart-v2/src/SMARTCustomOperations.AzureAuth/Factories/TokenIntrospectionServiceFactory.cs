using Microsoft.Extensions.DependencyInjection;
using SMARTCustomOperations.AzureAuth.Services;

namespace SMARTCustomOperations.AzureAuth.Factories
{
    class TokenIntrospectionServiceFactory : ITokenIntrospectionServiceFactory
    {
        private readonly IServiceProvider _provider;

        public TokenIntrospectionServiceFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public ITokenIntrospectionService GetService(string issuer)
        {
            if (issuer.Contains("sts.windows.net") || issuer.Contains("login.microsoftonline.com") || issuer.Contains("login.windows.net") || issuer.Contains("login.microsoft.com"))
                return _provider.GetRequiredService<EntraTokenIntrospectionService>();

            throw new NotSupportedException("Unsupported issuer");
        }
    }
}
