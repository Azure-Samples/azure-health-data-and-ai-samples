// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Azure.Core;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace SMARTCustomOperations.AzureAuth.Configuration
{
    /// <summary>
    /// Kiota <see cref="IAccessTokenProvider"/> that resolves tokens through an Azure <see cref="TokenCredential"/>
    /// (managed identity in Azure; <c>DefaultAzureCredential</c> chain locally).
    /// </summary>
    public class MicrosoftGraphAccessTokenProvider : IAccessTokenProvider
    {
        private readonly TokenCredential _credential;
        private readonly string[] _scopes;

        public MicrosoftGraphAccessTokenProvider(IOptions<GraphConfigurationOptions> options)
        {
            _credential = options.Value.Credential;
            _scopes = options.Value.Scopes;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; } = new AllowedHostsValidator();

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = default, CancellationToken cancellationToken = default)
        {
            var token = await _credential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken);
            return token.Token;
        }
    }
}
