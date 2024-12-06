namespace ClientApplication.Configuration
{
    public class config
    {
        public Uri FhirServerUrl { get; set; }

        public string role { get; set; } 

        public string role_id { get; set; }
        public string fhirproxy_roles { get; set; }

        public string key { get; set; }

        public string issuer { get; set; }

        public string audience { get; set; }
    }
}
