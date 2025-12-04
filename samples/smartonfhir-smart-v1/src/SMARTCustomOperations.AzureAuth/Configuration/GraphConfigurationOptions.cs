using Azure.Core;
using Azure.Identity;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    public class GraphConfigurationOptions
    {
        public TokenCredential Credential { get; set; } = new DefaultAzureCredential();

        public Uri AuthBaseUri { get; set; } = new Uri("https://graph.microsoft.com");

        public string[] Scopes { get; set; } = new string[] { "https://graph.microsoft.com/.default" };

    }
}
