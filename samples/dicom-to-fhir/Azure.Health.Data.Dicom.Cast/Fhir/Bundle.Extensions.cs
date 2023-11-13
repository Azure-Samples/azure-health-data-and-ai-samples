// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class BundleExtensions
{
    public static IAsyncEnumerable<Bundle.EntryComponent> GetEntriesAsync(this Bundle bundle, FhirClient client)
        => new AsyncBundleEnumerable(client, bundle);

    private sealed class AsyncBundleEnumerable : IAsyncEnumerable<Bundle.EntryComponent>
    {
        private readonly FhirClient _client;
        private readonly Bundle _root;

        public AsyncBundleEnumerable(FhirClient client, Bundle bundle)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _root = bundle ?? throw new ArgumentNullException(nameof(bundle));
        }

        public async IAsyncEnumerator<Bundle.EntryComponent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            Bundle? current = _root;
            do
            {
                foreach (Bundle.EntryComponent entry in current.Entry)
                    yield return entry;

                current = await _client.ContinueAsync(current, PageDirection.Next, cancellationToken);
            } while (current is not null && current.NextLink is not null);
        }
    }
}
