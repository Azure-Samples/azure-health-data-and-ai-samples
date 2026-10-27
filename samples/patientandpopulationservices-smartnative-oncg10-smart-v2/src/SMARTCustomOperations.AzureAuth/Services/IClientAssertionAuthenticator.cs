// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Specialized;

namespace SMARTCustomOperations.AzureAuth.Services
{
    /// <summary>
    /// Handles SMART v2 confidential asymmetric client authentication (<c>private_key_jwt</c>) for
    /// user-facing grants (<c>authorization_code</c>, <c>refresh_token</c>).
    ///
    /// Microsoft Entra ID cannot validate an arbitrary client-registered JWKS (RS384/ES384), so the
    /// gateway validates the inbound <c>client_assertion</c> itself and swaps it for the Entra
    /// <c>client_secret</c> stored in Key Vault. After the swap the request is an ordinary
    /// confidential-client request that Entra accepts. Client authentication is orthogonal to the
    /// grant type, so this single step serves both the standalone launch and its refresh.
    /// </summary>
    public interface IClientAssertionAuthenticator
    {
        /// <summary>
        /// Validates the <c>private_key_jwt</c> <c>client_assertion</c> in <paramref name="formData"/>
        /// and rewrites the collection in place: the assertion fields are removed and
        /// <c>client_id</c> + <c>client_secret</c> (from Key Vault) are set, so downstream
        /// classification treats the caller as a confidential client.
        /// </summary>
        /// <param name="formData">The parsed token-request form. Mutated in place on success.</param>
        /// <param name="expectedAudience">
        /// The token endpoint URL the assertion's <c>aud</c> must equal (the inbound <c>/api/token</c> URL).
        /// </param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the assertion is missing or invalid.</exception>
        Task SwapAssertionForClientSecretAsync(NameValueCollection formData, string expectedAudience);
    }
}
