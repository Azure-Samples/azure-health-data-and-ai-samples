// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text;

namespace SMARTCustomOperations.AzureAuth.Extensions
{
    /// <summary>
    /// Pure conversions between SMART on FHIR scope strings ("patient/Condition.rs") and the
    /// Entra scope form used on both the wire ("{audience}/patient.Condition.rs") and in
    /// the FHIR Resource App's oauth2PermissionScopes registration ("patient.Condition.rs").
    /// Callers pass a non-empty audience for the wire form, or empty for the registration form.
    /// </summary>
    public static class ScopeFormat
    {
        private static readonly string[] SmartPrefixes = { "patient", "encounter", "user", "system", "launch" };

        /// <summary>
        /// SMART -&gt; Entra. Example (with audience): "patient/Patient.rs" -&gt; "{audience}/patient.Patient.rs".
        /// Example (empty audience, i.e. registration form): "patient/Patient.rs" -&gt; "patient.Patient.rs".
        /// Non-SMART scopes (openid, offline_access, fhirUser-as-claim) pass through.
        /// </summary>
        public static string ToEntraFormat(string scopesString, string audience)
        {
            if (string.IsNullOrEmpty(scopesString))
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var aud = (audience ?? string.Empty).TrimEnd('/');

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
        /// Entra -&gt; SMART. Example: "{audience}/patient.Patient.rs" -&gt; "patient/Patient.rs".
        /// </summary>
        public static List<string> ToSmartFormat(IEnumerable<string> entraScopes, string audience)
        {
            var aud = (audience ?? string.Empty).TrimEnd('/');
            var result = new List<string>();
            foreach (var scope in entraScopes)
            {
                var s = string.IsNullOrEmpty(aud)
                    ? scope
                    : scope.Replace(aud, string.Empty, StringComparison.Ordinal);

                s = s.TrimStart('/')
                    .Replace("patient.", "patient/", StringComparison.Ordinal)
                    .Replace("encounter.", "encounter/", StringComparison.Ordinal)
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
            if (scope == "fhirUser")
            {
                return true;
            }

            foreach (var prefix in SmartPrefixes)
            {
                if (scope.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
