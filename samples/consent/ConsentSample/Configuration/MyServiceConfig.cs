namespace ConsentSample.Configuration
{
    public class MyServiceConfig
    {
        public Uri FhirServerUrl { get; set; }

        public string InstrumentationKey { get; set; }

        public string consent_category { get; set; }

        public string UserAgent { get; set; } = "fhir-migration-tool";

        public string SourceHttpClient { get; set; } = "SourceFhirEndpoint";
    }
}
