// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Text;

namespace SMARTCustomOperations.AzureAuth.Extensions
{
    public static class CookieExtensions
    {

        public static IDictionary<string, string> GetCookiesFromString(string cookieString = "")
        {
            var cookieDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var values = cookieString.TrimEnd(';').Split(';');
            foreach (var parts in values.Select(c => c.Split(new[] { '=' }, 2)))
            {
                var cookieName = parts[0].Trim();
                string cookieValue;

                if (parts.Length == 1)
                {
                    //Cookie attribute
                    cookieValue = string.Empty;
                }
                else
                {
                    cookieValue = parts[1];
                }

                cookieDictionary[cookieName] = cookieValue;
            }

            return cookieDictionary;
        }
    }
}