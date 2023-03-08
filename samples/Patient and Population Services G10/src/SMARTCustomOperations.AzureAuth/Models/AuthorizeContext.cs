// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;
using System.Web;
using SMARTCustomOperations.AzureAuth.Extensions;

namespace SMARTCustomOperations.AzureAuth.Models
{
    public class AuthorizeContext
    {
        private string _scope;

        private string _audience;

        public AuthorizeContext(NameValueCollection queryParams)
        {
            // Required
            ResponseType = queryParams["response_type"]!;
            ClientId = queryParams["client_id"]!;
            _scope = queryParams["scope"]!;
            _audience = queryParams["aud"]!;

            RedirectUri = queryParams.AllKeys.Contains("redirect_uri") ? new Uri(queryParams["redirect_uri"]!) : null;

            // Optional
            CodeChallenge = queryParams.AllKeys.Contains("code_challenge") ? queryParams["code_challenge"] : null;
            CodeChallengeMethod = queryParams.AllKeys.Contains("code_challenge_method") ? queryParams["code_challenge_method"] : null;
            State = queryParams.AllKeys.Contains("state") ? queryParams["state"] : null;
            Prompt = queryParams.AllKeys.Contains("prompt") ? queryParams["prompt"] : null;
            LoginHint = queryParams.AllKeys.Contains("login_hint") ? queryParams["login_hint"] : null;
        }

        public string ResponseType { get; }

        public string ClientId { get; }

        public Uri? RedirectUri { get; }

        public string Scope => _scope;

        public string? State { get; }

        public string Audience => _audience;

        public string? CodeChallenge { get; }

        public string? CodeChallengeMethod { get; }

        public string? Prompt { get; }

        public string? LoginHint { get; }

        public AuthorizeContext Translate(string fhirServerAud)
        {
            //_audience = fhirServerAud;
            _scope = Scope.ParseScope(fhirServerAud);
            return this;
        }

        public string ToRedirectQueryString()
        {
            List<string> queryStringParams = new();
            queryStringParams.Add($"response_type={ResponseType}");

            if (RedirectUri is not null)
            {
                queryStringParams.Add($"redirect_uri ={HttpUtility.UrlEncode(RedirectUri.ToString())}");
            }

            queryStringParams.Add($"client_id={HttpUtility.UrlEncode(ClientId)}");
            queryStringParams.Add($"scope={HttpUtility.UrlEncode(Scope)}");
            queryStringParams.Add($"state={HttpUtility.UrlEncode(State)}");
            queryStringParams.Add($"aud={HttpUtility.UrlEncode(Audience)}");

            if (!String.IsNullOrEmpty(CodeChallenge))
            {
                queryStringParams.Add($"code_challenge={HttpUtility.UrlEncode(CodeChallenge)}");
            }

            if (!String.IsNullOrEmpty(CodeChallengeMethod))
            {
                queryStringParams.Add($"code_challenge_method={HttpUtility.UrlEncode(CodeChallengeMethod)}");
            }

            if (!String.IsNullOrEmpty(Prompt))
            {
                queryStringParams.Add($"prompt={Prompt}");
            }

            if (!String.IsNullOrEmpty(LoginHint))
            {
                queryStringParams.Add($"login_hint={LoginHint}");
            }

            return string.Join("&", queryStringParams);
        }

        public bool Validate()
        {
            // TODO - Add config to force PKCE?
            if (string.IsNullOrEmpty(ResponseType) ||
                string.IsNullOrEmpty(ClientId) ||
                string.IsNullOrEmpty(RedirectUri?.ToString()) ||
                string.IsNullOrEmpty(Scope) ||
                string.IsNullOrEmpty(State) ||
                string.IsNullOrEmpty(Audience))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool IsValidResponseType()
        {
            if (!string.IsNullOrEmpty(ResponseType) &&
                ResponseType.ToLowerInvariant() == "code")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsValidAudience(string audience)
        {
            audience = audience.EndsWith("/") ? audience.TrimEnd("/".ToCharArray()) : audience;
            if (Audience.ToLowerInvariant() == audience.ToLowerInvariant())
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
