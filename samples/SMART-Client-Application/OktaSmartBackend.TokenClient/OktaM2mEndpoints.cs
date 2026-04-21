namespace OktaSmartBackend.TokenClient;

public static class OktaM2mEndpoints
{
    /// <summary>Okta OAuth 2.0 token endpoint for the given custom authorization server.</summary>
    public static string GetTokenEndpoint(string oktaDomain, string authServerId)
    {
        if (string.IsNullOrWhiteSpace(oktaDomain))
            throw new ArgumentException("Okta domain is required.", nameof(oktaDomain));
        if (string.IsNullOrWhiteSpace(authServerId))
            throw new ArgumentException("Authorization server ID is required.", nameof(authServerId));

        return $"{oktaDomain.TrimEnd('/')}/oauth2/{authServerId.Trim()}/v1/token";
    }
}
