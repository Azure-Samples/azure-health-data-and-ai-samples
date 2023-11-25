// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Health.Data.Dicom.Cast.Http;

internal sealed class AuthorizationHandler : DelegatingHandler
{
    private readonly TokenCredential _tokenCredential;
    private readonly Uri _scope;

    public AuthorizationHandler(TokenCredential tokenCredential, Uri scope)
    {
        _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken accessToken = _tokenCredential.GetToken(new TokenRequestContext(new[] { _scope.AbsoluteUri }), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return base.Send(request, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        AccessToken accessToken = await _tokenCredential.GetTokenAsync(new TokenRequestContext(new[] { _scope.AbsoluteUri }), cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return await base.SendAsync(request, cancellationToken);
    }
}
