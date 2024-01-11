// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Health.Data.Dicom.Cast.Http;

internal sealed class AuthorizationHandler(TokenCredential tokenCredential, Uri resource) : DelegatingHandler
{
    private readonly TokenCredential _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
    private readonly string[] _scopes = [new Uri(resource, ".default").AbsoluteUri];

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken accessToken = _tokenCredential.GetToken(new TokenRequestContext(_scopes), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return base.Send(request, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(_scopes), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
