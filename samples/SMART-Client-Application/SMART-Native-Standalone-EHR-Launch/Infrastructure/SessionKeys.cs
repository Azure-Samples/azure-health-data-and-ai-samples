namespace SmartOnFhirDemo.Infrastructure;

public static class SessionKeys
{
    public const string State = "smart.state";
    public const string CodeVerifier = "smart.code_verifier";
    public const string AccessToken = "smart.access_token";
    public const string RefreshToken = "smart.refresh_token";
    /// <summary>Raw JSON body from the token proxy (includes patient, encounter, need_patient_banner after augmentation).</summary>
    public const string LastTokenResponseJson = "smart.last_token_response_json";
    public const string LastError = "smart.last_error";
    public const string LastFhirResourceJson = "smart.last_fhir_json";
    public const string LastFhirResourceType = "smart.last_fhir_type";
    public const string LaunchType = "smart.launch_type";
    public const string LastSuccess = "smart.last_success";
    /// <summary>FHIR base URL provided by the EHR as the ISS parameter on EHR launch.</summary>
    public const string Iss = "smart.iss";

    // ── User Context (EHR Simulator lightweight identity login) ───────────────
    /// <summary>Access token obtained from the lightweight User Context OIDC login.
    /// Used only to authenticate the context-cache call — never for FHIR access.</summary>
    public const string UserContextToken = "smart.usercontext.token";
    /// <summary>OAuth2 state for the User Context OIDC flow.</summary>
    public const string UserContextState = "smart.usercontext.state";
    /// <summary>PKCE code verifier for the User Context OIDC flow.</summary>
    public const string UserContextCodeVerifier = "smart.usercontext.code_verifier";

    /// <summary>Pretty-printed JSON from Okta M2M token response (Backend Services flow).</summary>
    public const string BackendTokenResponseJson = "smart.backend_token_response_json";

    /// <summary>Access token from Backend Services client_credentials flow.</summary>
    public const string BackendAccessToken = "smart.backend_access_token";
}
