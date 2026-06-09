// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Models
{
    /// <summary>
    /// SMART v2 backend services token context (grant_type=client_credentials, private_key_jwt).
    /// The proxy validates the client_assertion locally (Entra cannot accept arbitrary JWKS)
    /// and then swaps to a stored Entra client_secret to invoke Entra's token endpoint.
    /// </summary>
    public class BackendServiceTokenContext : TokenContext
    {
        private static readonly JwtSecurityTokenHandler Handler = new();

        public BackendServiceTokenContext(NameValueCollection form)
        {
            if (form["grant_type"] != GrantType.client_credentials.ToString())
            {
                throw new ArgumentException("BackendServiceTokenContext requires the client_credentials grant type.");
            }

            GrantType = GrantType.client_credentials;
            ClientAssertionType = form["client_assertion_type"]!;
            ClientAssertion = form["client_assertion"]!;
            Scope = form["scope"];
        }

        public GrantType GrantType { get; }

        public string ClientAssertionType { get; }

        [JsonIgnore]
        public string ClientAssertion { get; }

        public string? Scope { get; set; }

        /// <summary>
        /// Derived from the JWT subject (SMART v2 requires iss=sub=client_id).
        /// </summary>
        public override string ClientId => Handler.ReadJwtToken(ClientAssertion).Subject;

        /// <summary>
        /// Backend services do not have a direct one-shot form conversion — the outbound
        /// Entra request requires the stored client_secret from KV. Use <see cref="BuildOutboundForm"/>.
        /// </summary>
        public override FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            throw new InvalidOperationException(
                "Use BuildOutboundForm(clientSecret, outboundScope) for BackendServiceTokenContext.");
        }

        /// <summary>
        /// Builds the outbound form for Entra's <c>client_credentials</c> flow using the
        /// KV-stored client_secret and the strategy-supplied scope (e.g. "{audience}/.default").
        /// </summary>
        public FormUrlEncodedContent BuildOutboundForm(string clientSecret, string outboundScope)
        {
            var values = new List<KeyValuePair<string, string>>
            {
                new("grant_type", GrantType.client_credentials.ToString()),
                new("client_id", ClientId),
                new("client_secret", clientSecret),
                new("scope", outboundScope),
            };

            return new FormUrlEncodedContent(values);
        }

        public override void Validate()
        {
            if (GrantType != GrantType.client_credentials ||
                string.IsNullOrEmpty(ClientAssertion) ||
                string.IsNullOrEmpty(ClientAssertionType))
            {
                throw new ArgumentException("BackendServiceTokenContext invalid");
            }
        }
    }
}
