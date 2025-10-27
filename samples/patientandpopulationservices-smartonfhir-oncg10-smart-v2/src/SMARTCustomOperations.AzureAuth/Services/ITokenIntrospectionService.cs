using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
    public interface ITokenIntrospectionService
    {
        public Task<TokenIntrospectionResult> IntrospectAsync(string token);
    }
}
