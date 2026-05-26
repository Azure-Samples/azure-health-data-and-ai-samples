namespace SMARTOnFhir.Server.Models
{
    public class AppConsentInfo
    {
        public string? ApplicationId { get; set; }
        public string? ApplicationName { get; set; }
        public string? ApplicationDescription { get; set; }
        public string? ApplicationUrl { get; set; }
        public List<AppConsentScope> Scopes { get; set; } = new List<AppConsentScope>();
    }

    public class AppConsentScope
    {
        public string? Name { get; set; }
        public string? Id { get; set; }
        public string? UserDescription { get; set; }
        public string? ResourceId { get; set; }
        public bool Consented { get; set; } = false;
        public string? ConsentId { get; set; }
    }

    public class LaunchCacheObject
    {
        public string? UserId { get; set; }
        public string? Launch { get; set; }
    }
}
