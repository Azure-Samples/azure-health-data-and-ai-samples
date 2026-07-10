using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class ClaimCacheObject
    {
        [JsonPropertyName("sub")]
        public string? Subject { get; set; }    // Subject (user ID)
        [JsonPropertyName("iss")]
        public string? Issuer { get; set; }    // Issuer (e.g., https://login.microsoftonline.com/{tenantId}/v2.0)
    }
}
