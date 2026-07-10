namespace SmartBackendServices.TokenClient;

/// <summary>
/// Result of a SMART v2 Backend Services token request (client_credentials + private_key_jwt).
/// </summary>
public sealed class SmartBackendTokenResult
{
    public bool IsSuccess { get; init; }

    public string? AccessToken { get; init; }

    public int? ExpiresIn { get; init; }

    public string? TokenType { get; init; }

    /// <summary>Full JSON body from the token endpoint (success or error).</summary>
    public string RawResponseBody { get; init; } = "";

    public int? HttpStatusCode { get; init; }

    /// <summary>Human-readable error for logging or UI when <see cref="IsSuccess"/> is false.</summary>
    public string? ErrorSummary { get; init; }
}
