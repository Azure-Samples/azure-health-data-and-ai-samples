// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text;

namespace SMARTCustomOperations.AzureAuth.Extensions
{
    public static class ScopeExtensions
    {
        public static string ParseScope(this string scopesString, string scopeAudience)
        {
            var scopesBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(scopesString))
            {
                var scopes = scopesString.Replace('+', ' ').Split(' ');

                foreach (var s in scopes)
                {
                    // if scope starts with patient/ or encounter/ or user/ or system/ or launch or equals fhirUser
                    if (s.StartsWith("patient", StringComparison.InvariantCulture) ||
                        s.StartsWith("encounter", StringComparison.InvariantCulture) ||
                        s.StartsWith("user", StringComparison.InvariantCulture) ||
                        s.StartsWith("system", StringComparison.InvariantCulture) ||
                        s.StartsWith("launch", StringComparison.InvariantCulture) ||
                        s == "fhirUser")
                    {
                        // Microsoft Entra ID v2.0 uses fully qualified scope URIs
                        // and does not allow '/'. Therefore, we need to
                        // replace '/' with '%2f' in the scope URI

                        var parts = s.Split('?', 2);
                        string resourcePart = parts[0];
                        string queryPart = parts.Length > 1 ? parts[1] : null;

                        var formattedScope = resourcePart.Replace("/", ".", StringComparison.InvariantCulture);

                        if (queryPart != null)
                        {
                            string encodedQuery = queryPart.Replace("/", "%2f", StringComparison.InvariantCulture);
                            formattedScope = $"{formattedScope}?{encodedQuery} ";
                        }
			
			formattedScope = formattedScope.Replace(".*", ".all", StringComparison.InvariantCulture);

                        // Leave the space in the string below
                        if (scopeAudience.EndsWith("/", StringComparison.InvariantCultureIgnoreCase) || scopeAudience.Length == 0)
                        {
                            formattedScope = $"{scopeAudience}{formattedScope} ";
                        }
                        else
                        {
                            formattedScope = $"{scopeAudience}/{formattedScope} ";
                        }

                        scopesBuilder.Append(formattedScope);
                    }
                    else
                    {
                        scopesBuilder.Append($"{s} ");
                    }
                }
            }

            var newScopes = scopesBuilder.ToString().TrimEnd();

            return newScopes;
        }
    }
}
