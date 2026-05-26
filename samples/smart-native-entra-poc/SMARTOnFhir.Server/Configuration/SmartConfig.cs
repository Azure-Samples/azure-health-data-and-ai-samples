namespace SMARTOnFhir.Server.Configuration
{
    public class SmartConfig
    {
        public string FhirServerUrl { get; set; } = string.Empty;
        public string AuthorityUrl { get; set; } = string.Empty;
        public string FhirAudience { get; set; } = string.Empty;
        public string ContextAppClientId { get; set; } = string.Empty;
        public string FhirResourceAppId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Controls whether the React consent UI is shown for STANDALONE launches that include a
        /// launch context scope ("launch", "launch/patient", "launch/encounter").
        ///   - false (default): no UI is ever shown. Standalone requests are forwarded directly to Entra.
        ///   - true:  standalone requests carrying a launch scope are redirected to /auth/context/ so
        ///     the user can confirm scopes (and identify patient/encounter when the UI supports it).
        ///     Standalone requests without a launch scope still go direct to Entra.
        ///
        /// EHR launches (requests carrying a non-empty "launch" parameter) are ALWAYS resolved
        /// server-side and NEVER touch the UI, regardless of this flag.
        ///
        /// Note: when the UI is disabled, Entra v1 may issue tokens whose "scp" claim contains
        /// all previously-consented scopes for the FHIR resource (not only the scopes requested
        /// in this round). This is documented Entra behavior. The FHIR API still enforces SMART
        /// scopes per-call. To get strict per-request scope narrowing, enable the React UI.
        /// </summary>
        public bool UseConsentUI { get; set; } = false;
    }
}
