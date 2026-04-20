// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text.Json;
using SMARTCustomOperations.AzureAuth.Extensions;

namespace SMARTCustomOperations.AzureAuth.Models
{
#pragma warning disable CA1707, SA1300
    public enum GrantType
    {
        authorization_code,
        refresh_token,
    }
#pragma warning restore CA1707, SA1300

    public abstract class TokenContext
    {
        public abstract string ClientId { get; }

        public virtual string ToLogString()
        {
            return JsonSerializer.Serialize(this);
        }

        public abstract void Validate();

        public abstract FormUrlEncodedContent ToFormUrlEncodedContent();

        /// <summary>
        /// Parses the form-encoded token request into the appropriate TokenContext subclass.
        /// For external IDPs, scopes pass through as-is (no translation needed).
        /// </summary>
        public static TokenContext FromFormUrlEncodedContent(NameValueCollection formData, AuthenticationHeaderValue? authHeaderValue)
        {
            AddBasicAuthData(formData, authHeaderValue);

            TokenContext? tokenContext = null;

            if (formData.AllKeys.Contains("grant_type") && formData["grant_type"] == GrantType.authorization_code.ToString())
            {
                if (formData.AllKeys.Contains("client_secret"))
                {
                    tokenContext = new ConfidentialClientTokenContext(formData);
                }
                else
                {
                    tokenContext = new PublicClientTokenContext(formData);
                }
            }
            else if (formData.AllKeys.Contains("grant_type") && formData["grant_type"] == GrantType.refresh_token.ToString())
            {
                tokenContext = new RefreshTokenContext(formData);
            }

            if (tokenContext is null)
            {
                throw new ArgumentException("Invalid token content");
            }

            return tokenContext;
        }

        private static void AddBasicAuthData(NameValueCollection formData, AuthenticationHeaderValue? reqAuth)
        {
            if (reqAuth is not null && reqAuth.Scheme == "Basic" && reqAuth.Parameter is not null)
            {
                formData.Remove("client_id");
                formData.Remove("client_secret");

                var authParameterDecoded = reqAuth.Parameter.DecodeBase64()!.Split(":");
                formData.Add("client_id", authParameterDecoded[0]);
                formData.Add("client_secret", authParameterDecoded[1]);
            }
        }
    }
}
