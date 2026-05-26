namespace SmartOnFhirDemo.Models;

public sealed class HomeViewModel
{
    public bool IsAuthenticated { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public string? Scopes { get; init; }
    public string? FhirUser { get; init; }
    /// <summary>Pretty-printed JSON from the token proxy response (patient, encounter, need_patient_banner, scope, etc.).</summary>
    public string? TokenResponseJson { get; init; }
    public string? LastError { get; init; }
    public string? LastSuccess { get; init; }
    public string? FhirResultJson { get; init; }
    public string? FhirResultType { get; init; }
    public string SelectedLaunchType { get; init; } = SmartOnFhirDemo.Infrastructure.LaunchType.Standalone;
    public string? SmartConfigRawJson { get; init; }
    /// <summary>FHIR resource type name e.g. "Patient" – triggers the result modal on load.</summary>
    public string? ShowFhirModal { get; init; }
    /// <summary>Pre-fills the ISS field in the EHR simulator panel with the configured FHIR base URL.</summary>
    public string? EhrSimulatorIssDefault { get; init; }
    /// <summary>True when the user has completed the User Context (EHR identity) login.
    /// Drives which step of the EHR simulator is shown in the UI.</summary>
    public bool HasUserContext { get; init; }

    /// <summary>Okta client_credentials response (Backend Services), pretty-printed JSON.</summary>
    public string? BackendM2mTokenJson { get; init; }

    /// <summary>True when backend-services token exists in session.</summary>
    public bool HasBackendToken { get; init; }
}
