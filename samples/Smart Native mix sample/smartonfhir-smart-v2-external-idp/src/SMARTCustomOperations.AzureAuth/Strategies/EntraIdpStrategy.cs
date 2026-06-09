// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text;
using SMARTCustomOperations.AzureAuth.Configuration;

namespace SMARTCustomOperations.AzureAuth.Strategies
{
    public sealed class EntraIdpStrategy : IIdpStrategy
    {
        private static readonly string[] SmartPrefixes = { "patient", "encounter", "user", "system", "launch" };

        private readonly AzureAuthOperationsConfig _config;

        public EntraIdpStrategy(AzureAuthOperationsConfig config)
        {
            _config = config;
        }

        public bool ProvidesAuthorizeProxy => true;

        public bool SupportsBackendServices => true;

        public Task<string> GetTokenEndpointAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/token");

        public Task<string> GetAuthorizeEndpointAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/oauth2/v2.0/authorize");

        public Task<string> GetOpenIdConfigurationUrlAsync() =>
            Task.FromResult($"https://login.microsoftonline.com/{_config.TenantId}/v2.0/.well-known/openid-configuration");

        public string TranslateScopesToIdp(string smartScopes) =>
            ToEntraFormat(smartScopes, _config.FhirAudience ?? string.Empty);

        public IEnumerable<string> TranslateScopesFromIdp(IEnumerable<string> idpScopes) =>
            ToSmartFormat(idpScopes, _config.FhirAudience ?? string.Empty);

        /// <summary>
        /// Entra only accepts "{resource}/.default" for client_credentials (AADSTS1002012).
        /// </summary>
        public string BuildBackendScope(string fhirAudience) =>
            $"{(fhirAudience ?? string.Empty).TrimEnd('/')}/.default";


        /// <summary>
        /// SMART -> Entra. Example: "patient/Patient.rs" -> "{audience}/patient.Patient.rs".
        /// Non-SMART scopes (openid, offline_access, fhirUser-as-claim) pass through.
        /// </summary>
        private static string ToEntraFormat(string scopesString, string audience)
        {
            if (string.IsNullOrEmpty(scopesString))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var aud = audience.TrimEnd('/');

            foreach (var s in scopesString.Replace('+', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsSmartScope(s))
                {
                    var parts = s.Split('?', 2);
                    var resourcePart = parts[0];
                    var queryPart = parts.Length > 1 ? parts[1] : null;

                    var formatted = resourcePart.Replace("/", ".", StringComparison.Ordinal);
                    if (queryPart != null)
                    {
                        var encodedQuery = queryPart.Replace("/", "%2f", StringComparison.Ordinal);
                        formatted = $"{formatted}?{encodedQuery}";
                    }

                    formatted = formatted.Replace(".*", ".all", StringComparison.Ordinal);

                    sb.Append(string.IsNullOrEmpty(aud) ? formatted : $"{aud}/{formatted}");
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(s);
                    sb.Append(' ');
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Entra -> SMART. Example: "{audience}/patient.Patient.rs" -> "patient/Patient.rs".
        /// </summary>
        private static List<string> ToSmartFormat(IEnumerable<string> entraScopes, string audience)
        {
            var aud = audience.TrimEnd('/');
            var result = new List<string>();
            foreach (var scope in entraScopes)
            {
                var s = string.IsNullOrEmpty(aud)
                    ? scope
                    : scope.Replace(aud, string.Empty, StringComparison.Ordinal);

                s = s.TrimStart('/')
                    .Replace("patient.", "patient/", StringComparison.Ordinal)
                    .Replace("user.", "user/", StringComparison.Ordinal)
                    .Replace("system.", "system/", StringComparison.Ordinal)
                    .Replace("launch.", "launch/", StringComparison.Ordinal)
                    .Replace("%2f", "/", StringComparison.Ordinal)
                    .Replace(".all", ".*", StringComparison.Ordinal);

                result.Add(s);
            }

            return result;
        }

        private static bool IsSmartScope(string scope)
        {
            if (scope == "fhirUser") return true;
            foreach (var prefix in SmartPrefixes)
            {
                if (scope.StartsWith(prefix, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
