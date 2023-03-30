using Newtonsoft.Json;
using SMARTCustomOperations.AzureAuth.Extensions;

namespace SMARTCustomOperations.AzureAuth.Models
{
    /// <summary>
    /// Data storage object used to client launch information.
    /// </summary>
    public class LaunchCacheObject
    {
        private Dictionary<string, string>? _launchProperties;

        public string? UserId { get; set; }

        public string? Launch { get; set; }

        public Dictionary<string, string>? LaunchProperties { 
            get 
            {
                if (_launchProperties is not null)
                {
                    return _launchProperties;
                }

                if (Launch is not null)
                {
                    var launchDecoded = Launch.DecodeBase64();
                    if (launchDecoded is not null)
                    {
                        
                        _launchProperties = JsonConvert.DeserializeObject<Dictionary<string, string>>(launchDecoded);
                    }
                }

                return _launchProperties;
            }
        }
    }
}
