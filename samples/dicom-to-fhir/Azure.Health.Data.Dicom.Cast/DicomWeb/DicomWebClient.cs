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

internal class DicomWebClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _options;

    private const string ApplicationDicomJson = "application/dicom+json";

    public DicomWebClient(HttpClient httpClient, IOptionsSnapshot<JsonSerializerOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<DicomDataset?> RetrieveInstanceMetadataAsync(
        string studyInstanceUid,
        string seriesInstanceUid,
        string sopInstanceUid,
        CancellationToken cancellationToken = default)
    {
        Uri route = new($"study/{studyInstanceUid}/series/{seriesInstanceUid}/instance/{sopInstanceUid}/metadata", UriKind.Relative);
        using HttpRequestMessage request = new(HttpMethod.Get, route)
        {
            Headers =
            {
                Accept = { new MediaTypeWithQualityHeaderValue(ApplicationDicomJson) }
            }
        };

        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        return await response
            .EnsureSuccessStatusCode()
            .Content
            .ReadFromJsonAsync<DicomDataset>(_options, cancellationToken);
    }
}
