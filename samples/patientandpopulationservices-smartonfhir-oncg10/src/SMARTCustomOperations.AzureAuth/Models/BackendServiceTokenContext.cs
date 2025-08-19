// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class BackendServiceTokenContext : TokenContext
    {
        private static JwtSecurityTokenHandler _handler = new JwtSecurityTokenHandler();

        /// <summary>
        /// Creates a RefreshTokenContext from the NameValueCollection from the HTTP request body.
        /// </summary>
        /// <param name="form">HTTP Form Encoded Body from Token Request.</param>
        /// <param name="audience">Microsoft Entra ID audience for the FHIR Server.</param>
        public BackendServiceTokenContext(NameValueCollection form, string audience)
        {
            if (form["grant_type"] != GrantType.authorization_code.ToString() && form["grant_type"] != GrantType.client_credentials.ToString())
            {
                throw new ArgumentException("RefreshTokenContext requires the client_credentials grant type.");
            }

            if (form["grant_type"] == GrantType.authorization_code.ToString())
            {
                GrantType = GrantType.authorization_code;
                Code = form["code"]!;
                CodeVerifier = form["code_verifier"]!;

                if (form.AllKeys.Contains("redirect_uri"))
                {
                    RedirectUri = new Uri(form["redirect_uri"]!);
                }
            }
            else
            {
                GrantType = GrantType.client_credentials;
            }
            // Since there is no user interaction involved, Microsoft Entra ID only accepts the .default scope. It will give
            // the application the approved scopes.
            // AADSTS1002012
            Scope = $"{audience}/.default";
            ClientAssertionType = form["client_assertion_type"]!;
            ClientAssertion = form["client_assertion"]!;
        }

        public GrantType GrantType { get; }

        public string Code { get; } = default!;

        public Uri RedirectUri { get; } = default!;

        public string? CodeVerifier { get; } = default!;

        public string Scope { get; }

        public string ClientAssertionType { get; }

        public string ClientAssertion { get; }

        public override string ClientId => _handler.ReadJwtToken(ClientAssertion).Subject;

        public override FormUrlEncodedContent ToFormUrlEncodedContent()
        {
            throw new InvalidOperationException("ClientConfidentialAsync cannot be encoded to Form URL Content since this flow does not interact with Microsoft Entra ID.");
        }

        public override void Validate()
        {
            if ((GrantType != GrantType.client_credentials && GrantType != GrantType.authorization_code) ||
                //string.IsNullOrEmpty(Scope) ||
                string.IsNullOrEmpty(ClientAssertionType) ||
                string.IsNullOrEmpty(ClientAssertion))
            {
                throw new ArgumentException("BackendServiceTokenContext invalid");
            }
        }

        public FormUrlEncodedContent ConvertToClientCredentialsFormUrlEncodedContent(string clientSecret)
        {
            //List<KeyValuePair<string, string>> formValues = new()
            //{
            //    new KeyValuePair<string, string>("grant_type", "client_credentials"),
            //    new KeyValuePair<string, string>("scope", Scope),
            //    new KeyValuePair<string, string>("client_id", ClientId),
            //    new KeyValuePair<string, string>("client_secret", clientSecret)
            //};

            List<KeyValuePair<string, string>> formValues = new()
            {
                new KeyValuePair<string, string>("code", Code),
                new KeyValuePair<string, string>("grant_type", GrantType.ToString()),
                new KeyValuePair<string, string>("redirect_uri", RedirectUri.ToString()),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("client_secret", clientSecret)
            };

            if (!String.IsNullOrEmpty(CodeVerifier))
            {
                formValues.Add(new KeyValuePair<string, string>("code_verifier", CodeVerifier));
            }

            return new FormUrlEncodedContent(formValues);
        }
    }
}
