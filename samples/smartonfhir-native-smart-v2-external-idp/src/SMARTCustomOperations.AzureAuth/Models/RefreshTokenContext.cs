// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Text.Json;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class RefreshTokenContext : TokenContext
    {
        public RefreshTokenContext(NameValueCollection form)
        {
            if (form["grant_type"] != GrantType.refresh_token.ToString())
            {
                throw new ArgumentException("RefreshTokenContext requires the refresh token type.");
            }

            GrantType = GrantType.refresh_token;
            ClientId = form["client_id"]!;
            RefreshToken = form["refresh_token"]!;
            ClientSecret = form["client_secret"];
            Scope = form["scope"];
            ClientAssertionType = form["client_assertion_type"];
            ClientAssertion = form["client_assertion"];
        }

        public GrantType GrantType { get; }

        public string RefreshToken { get; }

        public string? Scope { get; }

        public override string ClientId { get; }

        public string? ClientSecret { get; }

        public string? ClientAssertionType { get; }

        public string? ClientAssertion { get; }

        public override string ToLogString()
        {
            return JsonSerializer.Serialize(new { GrantType, ClientId, RefreshToken = "***", Scope });
        }

        public override FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            var formValues = new List<KeyValuePair<string, string>>
            {
                new("client_id", ClientId),
                new("refresh_token", RefreshToken),
                new("grant_type", GrantType.ToString()),
            };

            if (Scope is not null)
                formValues.Add(new("scope", Scope));

            if (ClientSecret is not null)
                formValues.Add(new("client_secret", ClientSecret));

            if (ClientAssertionType is not null)
                formValues.Add(new("client_assertion_type", ClientAssertionType));

            if (ClientAssertion is not null)
                formValues.Add(new("client_assertion", ClientAssertion));

            return new FormUrlEncodedContent(formValues);
        }

        public override void Validate()
        {
            if (GrantType != GrantType.refresh_token ||
                string.IsNullOrEmpty(ClientId) ||
                string.IsNullOrEmpty(RefreshToken))
            {
                throw new ArgumentException("Refresh TokenContext invalid");
            }
        }
    }
}
