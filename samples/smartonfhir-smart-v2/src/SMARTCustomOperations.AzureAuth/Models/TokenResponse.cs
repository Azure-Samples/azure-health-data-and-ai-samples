
using Google.Protobuf;
using Microsoft.AzureHealth.DataServices.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SMARTCustomOperations.AzureAuth.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class TokenResponse
    {
        private readonly AzureAuthOperationsConfig _configuration;

        private readonly string _tokenResponseString;

        private readonly Dictionary<string, object> _tokenResponseDict;

        private IEnumerable<string>? _scopes;

        private string? _userId;

        private string? _appId;

        public TokenResponse(AzureAuthOperationsConfig configuration, string tokenResponseString)
        {
            _configuration = configuration;
            _tokenResponseString = tokenResponseString;
            _tokenResponseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(tokenResponseString) ?? new Dictionary<string, object>();
        }

        public IEnumerable<string> Scope
        {
            get
            {
                if (_scopes is null)
                {
                    _scopes = GetFhirScopes();
                }

                return _scopes;
            }
        }

        public string? FhirUser
        {
            get
            {
                if (_tokenResponseDict.ContainsKey("id_token"))
                {
                    JwtSecurityToken parsedIdToken = new JwtSecurityToken(_tokenResponseDict["id_token"].ToString());
                    return parsedIdToken.Claims.FirstOrDefault(x => x.Type == "fhirUser")?.Value;
                }

                return null;
            }
        }

        public string? UserId
        {
            get
            {
                if (_userId is null)
                {
                    if (_tokenResponseDict.ContainsKey("access_token"))
                    {
                        JwtSecurityToken parsedAccessToken = new JwtSecurityToken(_tokenResponseDict["access_token"].ToString());
                        _userId = parsedAccessToken.Claims.FirstOrDefault(x => x.Type == "oid")?.Value;
                    }
                }
                
                return _userId;
            }
        }

        public string? AppId
        {
            get
            {
                if (_appId is null)
                {
                    if (_tokenResponseDict.ContainsKey("access_token"))
                    {
                        JwtSecurityToken parsedAccessToken = new JwtSecurityToken(_tokenResponseDict["access_token"].ToString());
                        _appId = parsedAccessToken.Claims.FirstOrDefault(x => x.Type == "appid")?.Value;
                    }
                }
                
                return _appId;
            }
        }

        public void AddCustomProperty(string key, object value)
        {
            _tokenResponseDict[key] = value;
        }

        public bool TryGetIdToken(out string id_token)
        {
            if (_tokenResponseDict.ContainsKey("id_token"))
            {
                id_token = _tokenResponseDict["id_token"].ToString() ?? string.Empty;
                return true;
            }
            id_token = string.Empty;
            return false;
        }

        public override string ToString()
        {
            var output = _tokenResponseDict;

            output["scope"] = String.Join(' ', Scope);

            // If the app was launched by a patient, restrict their access to themselves.
            // #TODO - if implementing a patient picker, this logic will need to be changed.
            var patientField = GetPatientFromFhirUser();
            if (patientField is not null)
            {
                output["patient"] = patientField;
            }

            // Add EHR styling information
            if (Scope.Any(x => x == "launch"))
            {
                // #TODO - change to match the logic in your EHR application
                output["need_patient_banner"] = true;
                output["smart_style_url"] = $"https://{_configuration.ApiManagementHostName}/smart/smart-style.json";
            }
            
            return JsonConvert.SerializeObject(output);
        }

        private List<string> GetFhirScopes()
        {
            
            if (!_tokenResponseDict.ContainsKey("access_token"))
            {
                return Enumerable.Empty<string>().ToList();
            }

            var accessToken = _tokenResponseDict["access_token"]!.ToString();
;
            var decodedAccessToken = new JwtSecurityToken(accessToken);
            var scopes = decodedAccessToken.Claims.FirstOrDefault(x => x.Type == "scp")?.Value.Split(' ');
            var roles = decodedAccessToken.Claims.Where(x => x.Type == "roles").Select(x => x.Value);

            List<string> transformedScopes = new();

            foreach (var scope in scopes ?? roles)
            {
                // These must be case-sensitive to perserve scopes like patient/Patient.read.
                transformedScopes.Add(scope
                    .Replace(_configuration.FhirAudience!, string.Empty, StringComparison.InvariantCulture)
                    .TrimStart('/')
                    .Replace("patient.", "patient/", StringComparison.InvariantCulture)
                    .Replace("user.", "user/", StringComparison.InvariantCulture)
                    .Replace("system.", "system/", StringComparison.InvariantCulture)
                    .Replace("launch.", "launch/", StringComparison.InvariantCulture)
                    .Replace("%2f", "/", StringComparison.InvariantCulture)
                    .Replace("all", "*")
                );
            }

            if (_tokenResponseDict.ContainsKey("id_token"))
            {
                transformedScopes.Add("openid");
            }

            if (_tokenResponseDict.ContainsKey("refresh_token"))
            {
                transformedScopes.Add("offline_access");
            }

            return transformedScopes;
        }

        private string? GetPatientFromFhirUser()
        {
            if (!String.IsNullOrEmpty(FhirUser))
            {
                var fhirUserSplit = FhirUser.Split('/');

                if (fhirUserSplit.Length >= 2)
                {
                    var localFhirUser = fhirUserSplit.Skip(Math.Max(0, fhirUserSplit.Count() - 2));
                    if (String.Equals(localFhirUser.First(), "patient", StringComparison.InvariantCultureIgnoreCase))
                    {
                        return localFhirUser.Last();
                    }
                }
            }

            return null;
        }
    }
}
