namespace SmartBackendServices.TokenClient;

/// <summary>
/// Helpers for building OAuth 2.0 token endpoint URLs for specific IdPs.
/// In normal SMART v2 usage prefer the <c>token_endpoint</c> from the FHIR server's
/// <c>.well-known/smart-configuration</c> discovery document instead.
/// </summary>
public static class SmartBackendEndpoints
{
    /// <summary>Okta OAuth 2.0 token endpoint for the given custom authorization server.</summary>
    public static string GetOktaTokenEndpoint(string oktaDomain, string authServerId)
    {
        if (string.IsNullOrWhiteSpace(oktaDomain))
            throw new ArgumentException("Okta domain is required.", nameof(oktaDomain));
        if (string.IsNullOrWhiteSpace(authServerId))
            throw new ArgumentException("Authorization server ID is required.", nameof(authServerId));

        return $"{oktaDomain.TrimEnd('/')}/oauth2/{authServerId.Trim()}/v1/token";
    }
}
