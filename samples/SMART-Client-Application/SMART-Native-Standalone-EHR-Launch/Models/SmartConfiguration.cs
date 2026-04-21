using System.Text.Json.Serialization;

public sealed class SmartConfiguration
{
    [JsonPropertyName("authorization_endpoint")]
    public string? AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; set; }

    [JsonPropertyName("scopes_supported")]
    public string[]? ScopesSupported { get; set; }
}
