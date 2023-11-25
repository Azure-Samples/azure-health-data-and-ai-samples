// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class BundleExtensions
{
    public static Bundle EnsureSuccessStatusCodes(this Bundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        foreach (Bundle.EntryComponent entry in bundle.Entry)
        {
            Bundle.ResponseComponent response = entry.Response;
            if (response.Status is null ||
                response.Status.Length < 3 ||
                !Enum.TryParse(response.Status[..3], out HttpStatusCode statusCode))
            {
                throw new FormatException("Cannot parse Response.Status into a valid HTTP status code.");
            }

            if (statusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
                throw new FhirOperationException($"Received invalid status {response.Status} for resource type {entry.Resource.TypeName}", statusCode);
        }

        return bundle;
    }

    public static IAsyncEnumerable<Bundle> GetPagesAsync(this Bundle bundle, FhirClient client)
        => new AsyncBundleEnumerable(client, bundle);

    private sealed class AsyncBundleEnumerable(FhirClient client, Bundle bundle) : IAsyncEnumerable<Bundle>
    {
        private readonly FhirClient _client = client ?? throw new ArgumentNullException(nameof(client));
        private readonly Bundle _root = bundle ?? throw new ArgumentNullException(nameof(bundle));

        public async IAsyncEnumerator<Bundle> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Bundle? current = _root;
            do
            {
                yield return current;
                current = await _client.ContinueAsync(current, PageDirection.Next, cancellationToken);
            } while (current is not null && current.NextLink is not null);
        }
    }
}
