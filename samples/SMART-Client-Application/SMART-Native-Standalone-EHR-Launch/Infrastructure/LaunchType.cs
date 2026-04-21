namespace SmartOnFhirDemo.Infrastructure;

/// <summary>
/// SMART on FHIR v2 launch types supported by this demo application.
/// </summary>
public static class LaunchType
{
    /// <summary>
    /// Standalone launch — the app launches independently and requests
    /// its own patient context via the launch/patient scope.
    /// </summary>
    public const string Standalone = "standalone";

    /// <summary>
    /// Standalone launch for a confidential client — same redirect flow
    /// as Standalone, but the token exchange includes client_secret.
    /// </summary>
    public const string StandaloneConfidential = "standalone-confidential";

    /// <summary>
    /// EHR launch — the EHR system launches the app and supplies a
    /// launch token and ISS parameter. The app exchanges these for context.
    /// </summary>
    public const string Ehr = "ehr";

    /// <summary>
    /// Backend services — machine-to-machine flow using client credentials
    /// with a signed JWT assertion. No user context or redirect involved.
    /// </summary>
    public const string Backend = "backend";

    public static bool IsValid(string? value) =>
        value is Standalone or StandaloneConfidential or Ehr or Backend;
}
