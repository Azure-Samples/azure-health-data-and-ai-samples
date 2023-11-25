// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Microsoft.Extensions.Options;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

internal sealed class DicomWebClient(HttpClient httpClient, IOptionsSnapshot<JsonSerializerOptions> options)
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly JsonSerializerOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

    private const string ApplicationDicomJson = "application/dicom+json";

    public async ValueTask<DicomDataset> RetrieveInstanceMetadataAsync(InstanceIdentifiers identifiers, CancellationToken cancellationToken = default)
    {
        Uri route = new($"study/{identifiers.StudyInstanceUid}/series/{identifiers.SeriesInstanceUid}/instance/{identifiers.SopInstanceUid}/metadata", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Get, route)
        {
            Headers =
            {
                Accept = { new MediaTypeWithQualityHeaderValue(ApplicationDicomJson) }
            }
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        return (await response
            .EnsureSuccessStatusCode()
            .Content
            .ReadFromJsonAsync<DicomDataset>(_options, cancellationToken))!;
    }
}
