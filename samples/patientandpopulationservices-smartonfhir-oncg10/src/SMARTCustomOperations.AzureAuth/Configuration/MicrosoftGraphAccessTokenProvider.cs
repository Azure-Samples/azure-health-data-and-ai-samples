using Azure.Core;
using Microsoft.AzureHealth.DataServices.Security;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public class MicrosoftGraphAccessTokenProvider : IAccessTokenProvider
    {       
        private readonly BearerTokenHandler _bearerTokenHandler;

        public MicrosoftGraphAccessTokenProvider(IOptions<GraphConfigurationOptions> options)
        {
            _bearerTokenHandler = new BearerTokenHandler(options.Value.Credential, options.Value.AuthBaseUri, options.Value.Scopes);
            AllowedHostsValidator = new AllowedHostsValidator();
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = default, CancellationToken cancellationToken = default)
        {
            var token = await _bearerTokenHandler.GetTokenAsync(cancellationToken);
            return token.Token;
        }
    }
}
