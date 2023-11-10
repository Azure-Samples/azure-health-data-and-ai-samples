using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

internal class DicomDelegatingHandler : DelegatingHandler
{
    private readonly DicomTokenCredential _tokenCredential;

    public DicomDelegatingHandler(DicomTokenCredential tokenCredential)
        => _tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // In-memory caching is available and enabled by default for most TokenCredential types
        AccessToken token = _tokenCredential.GetToken(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return base.SendAsync(request, cancellationToken);
    }
}
