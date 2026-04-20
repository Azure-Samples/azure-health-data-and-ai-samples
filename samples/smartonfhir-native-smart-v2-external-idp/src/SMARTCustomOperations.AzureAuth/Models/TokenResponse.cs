// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;

namespace SMARTCustomOperations.AzureAuth.Models
{
    /// <summary>
    /// Wraps the IDP's token response and augments it with SMART-specific fields.
    /// For external IDPs (Okta, Ping, etc.) scopes are already in SMART format — no translation needed.
    /// The augmentation adds: patient, fhirUser, need_patient_banner, smart_style_url, and launch context.
    /// </summary>
    public class TokenResponse
    {
        private readonly Dictionary<string, object> _tokenResponseDict;
        private readonly string _userIdClaimType;
        private string? _userId;

        /// <param name="userIdClaimType">
        /// JWT claim used as the Redis cache key for EHR launch context.
        /// Must match AzureAuthOperationsConfig.UserIdClaimType and ContextCacheInputFilter when storing context.
        /// </param>
        public TokenResponse(string tokenResponseString, string? userIdClaimType = null)
        {
            _tokenResponseDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(tokenResponseString)
                ?? new Dictionary<string, object>();
            _userIdClaimType = string.IsNullOrWhiteSpace(userIdClaimType) ? "sub" : userIdClaimType;
        }

        public string? FhirUser
        {
            get
            {
                if (_tokenResponseDict.ContainsKey("access_token"))
                {
                    var parsedToken = new JwtSecurityToken(_tokenResponseDict["access_token"].ToString());
                    return parsedToken.Claims.FirstOrDefault(x => x.Type == "fhirUser")?.Value;
                }

                if (_tokenResponseDict.ContainsKey("id_token"))
                {
                    var parsedToken = new JwtSecurityToken(_tokenResponseDict["id_token"].ToString());
                    return parsedToken.Claims.FirstOrDefault(x => x.Type == "fhirUser")?.Value;
                }

                return null;
            }
        }

        public string? UserId
        {
            get
            {
                if (_userId is null && _tokenResponseDict.ContainsKey("access_token"))
                {
                    var parsedToken = new JwtSecurityToken(_tokenResponseDict["access_token"].ToString());
                    // Primary: configured claim — MUST match ContextCacheInputFilter / Redis key.
                    _userId = parsedToken.Claims.FirstOrDefault(x => x.Type == _userIdClaimType)?.Value;
                    // Fallbacks only if the configured claim is absent (legacy / misconfiguration).
                    if (string.IsNullOrEmpty(_userId))
                    {
                        _userId = parsedToken.Claims.FirstOrDefault(x => x.Type == "uid")?.Value
                               ?? parsedToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value
                               ?? parsedToken.Claims.FirstOrDefault(x => x.Type == "oid")?.Value;
                    }
                }

                return _userId;
            }
        }

        public IEnumerable<string> Scopes => GetScopes();

        public void AddCustomProperty(string key, object value)
        {
            _tokenResponseDict[key] = value;
        }

        public override string ToString()
        {
            var output = _tokenResponseDict;

            output["scope"] = string.Join(' ', Scopes);

            // Do not overwrite patient / fhirUser already merged from EHR launch context (Redis).
            // Okta often emits patient + fhirUser on the access token; those would otherwise replace
            // the values pushed via context-cache (e.g. PatientA from simulator vs PatientB on JWT).
            if (!output.ContainsKey("patient"))
            {
                var patientId = GetPatientFromToken() ?? GetPatientFromFhirUser();
                if (patientId is not null)
                    output["patient"] = patientId;
            }

            if (!output.ContainsKey("fhirUser") && !string.IsNullOrEmpty(FhirUser))
                output["fhirUser"] = FhirUser;

            if (Scopes.Any(x => x == "launch"))
            {
                output["need_patient_banner"] = true;
            }

            return JsonConvert.SerializeObject(output);
        }

        /// <summary>
        /// Merges scopes from the JWT "scp" claim and the IDP response body "scope" field.
        /// Handles both Okta array (multiple claims) and single space-delimited string formats.
        /// </summary>
        private List<string> GetScopes()
        {
            HashSet<string> scopes = new(StringComparer.Ordinal);

            if (_tokenResponseDict.ContainsKey("access_token"))
            {
                var accessToken = _tokenResponseDict["access_token"]!.ToString();
                var decodedToken = new JwtSecurityToken(accessToken);

                var allScpClaims = decodedToken.Claims.Where(x => x.Type == "scp").Select(x => x.Value).ToList();

                if (allScpClaims.Count == 1)
                {
                    foreach (var s in allScpClaims[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        scopes.Add(s);
                }
                else if (allScpClaims.Count > 1)
                {
                    foreach (var s in allScpClaims)
                        scopes.Add(s);
                }
                else
                {
                    foreach (var s in decodedToken.Claims.Where(x => x.Type == "roles").Select(x => x.Value))
                        scopes.Add(s);
                }
            }

            if (_tokenResponseDict.ContainsKey("scope"))
            {
                var scopeValue = _tokenResponseDict["scope"]?.ToString();
                if (!string.IsNullOrEmpty(scopeValue))
                {
                    foreach (var s in scopeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        scopes.Add(s);
                }
            }

            if (_tokenResponseDict.ContainsKey("id_token") && !scopes.Contains("openid"))
            {
                scopes.Add("openid");
            }

            if (_tokenResponseDict.ContainsKey("refresh_token") && !scopes.Contains("offline_access"))
            {
                scopes.Add("offline_access");
            }

            return scopes.ToList();
        }

        private string? GetPatientFromToken()
        {
            if (!_tokenResponseDict.ContainsKey("access_token"))
                return null;

            var parsedToken = new JwtSecurityToken(_tokenResponseDict["access_token"].ToString());
            return parsedToken.Claims.FirstOrDefault(x => x.Type == "patient")?.Value;
        }

        private string? GetPatientFromFhirUser()
        {
            if (string.IsNullOrEmpty(FhirUser))
                return null;

            var segments = FhirUser.Split('/');
            if (segments.Length >= 2)
            {
                var lastTwoSegments = segments.Skip(Math.Max(0, segments.Length - 2)).ToArray();
                if (string.Equals(lastTwoSegments[0], "Patient", StringComparison.InvariantCultureIgnoreCase))
                {
                    return lastTwoSegments[1];
                }
            }

            return null;
        }
    }
}
