using System.Text;

namespace SMARTOnFhir.Server.Services
{
    public static class SmartScopeMapper
    {
        private static readonly string[] SmartPrefixes = { "patient", "encounter", "user", "system", "launch" };

        /// <summary>
        /// Converts SMART scopes to Entra ID format.
        /// Example: "patient/Patient.rs" → "{audience}/patient.Patient.rs"
        /// </summary>
        public static string ToEntraFormat(string scopesString, string audience)
        {
            var scopesBuilder = new StringBuilder();

            if (string.IsNullOrEmpty(scopesString))
            {
                return string.Empty;
            }

            var scopes = scopesString.Replace('+', ' ').Split(' ');

            foreach (var s in scopes)
            {
                if (IsSmartScope(s))
                {
                    var parts = s.Split('?', 2);
                    string resourcePart = parts[0];
                    string? queryPart = parts.Length > 1 ? parts[1] : null;

                    var formattedScope = resourcePart.Replace("/", ".", StringComparison.InvariantCulture);

                    if (queryPart != null)
                    {
                        string encodedQuery = queryPart.Replace("/", "%2f", StringComparison.InvariantCulture);
                        formattedScope = $"{formattedScope}?{encodedQuery} ";
                    }

                    formattedScope = formattedScope.Replace(".*", ".all", StringComparison.InvariantCulture);

                    if (audience.EndsWith("/", StringComparison.InvariantCultureIgnoreCase) || audience.Length == 0)
                    {
                        formattedScope = $"{audience}{formattedScope} ";
                    }
                    else
                    {
                        formattedScope = $"{audience}/{formattedScope} ";
                    }

                    scopesBuilder.Append(formattedScope);
                }
                else
                {
                    scopesBuilder.Append($"{s} ");
                }
            }

            return scopesBuilder.ToString().TrimEnd();
        }

        /// <summary>
        /// Converts Entra ID scopes to SMART format.
        /// Example: "{audience}/patient.Patient.rs" → "patient/Patient.rs"
        /// </summary>
        public static List<string> ToSmartFormat(IEnumerable<string> entraScopes, string audience)
        {
            var transformedScopes = new List<string>();

            foreach (var scope in entraScopes)
            {
                transformedScopes.Add(scope
                    .Replace(audience, string.Empty, StringComparison.InvariantCulture)
                    .TrimStart('/')
                    .Replace("patient.", "patient/", StringComparison.InvariantCulture)
                    .Replace("user.", "user/", StringComparison.InvariantCulture)
                    .Replace("system.", "system/", StringComparison.InvariantCulture)
                    .Replace("launch.", "launch/", StringComparison.InvariantCulture)
                    .Replace("%2f", "/", StringComparison.InvariantCulture)
                    .Replace("all", "*")
                );
            }

            return transformedScopes;
        }

        private static bool IsSmartScope(string scope)
        {
            if (scope == "fhirUser") return true;

            foreach (var prefix in SmartPrefixes)
            {
                if (scope.StartsWith(prefix, StringComparison.InvariantCulture))
                    return true;
            }

            return false;
        }
    }
}
