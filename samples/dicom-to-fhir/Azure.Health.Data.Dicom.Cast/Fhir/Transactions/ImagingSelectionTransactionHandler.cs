// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class ImagingSelectionTransactionHandler(FhirClient client, ILogger<ObservationTransactionHandler> logger)
{
    private readonly FhirClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ILogger<ObservationTransactionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<ResourceTransactionBuilder<ImagingSelection>> AddOrUpdateImagingSelectionAsync(
        TransactionBuilder builder,
        DicomDataset dataset,
        Endpoint endpoint,
        Patient patient,
        ImagingStudy study,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(dataset);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(study);

        Identifier identifier = dataset.GetSopInstanceIdentifier();
        ImagingSelection? imagingSelection = await GetImagingSelectionOrDefault(identifier, cancellationToken);
        if (imagingSelection is null)
        {
            imagingSelection = new()
            {
                Endpoint = [endpoint.GetReference()],
                DerivedFrom = [study.GetReference()],
                Identifier = [identifier],
                Meta = new Meta { Source = endpoint.Address },
                Status = ImagingSelection.ImagingSelectionStatus.Available,
                Subject = patient.GetReference(),
                Issued = DateTime.UtcNow,
            };

            imagingSelection = UpdateDicomSopInstance(imagingSelection, dataset);
            builder = builder.Create(imagingSelection, new SearchParams().Add(identifier));
        }
        else
        {
            imagingSelection = UpdateDicomSopInstance(imagingSelection, dataset);
            builder = builder.Update(new SearchParams(), imagingSelection, imagingSelection.Meta.VersionId);
        }

        return builder.ForResource(imagingSelection);
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance method for symmetry.")]
    public TransactionBuilder DeleteImagingSelection(TransactionBuilder builder, InstanceIdentifiers identifiers)
    {
        ArgumentNullException.ThrowIfNull(builder);

        Identifier identifier = DicomIdentifier.FromUid(identifiers.SopInstanceUid);
        return builder.Delete(nameof(ResourceType.ImagingSelection), new SearchParams().Add(identifier));
    }

    private async ValueTask<ImagingSelection?> GetImagingSelectionOrDefault(Identifier identifier, CancellationToken cancellationToken = default)
    {
        // Use SOP Instance UID as the identifier for ImagingSelection resources that refer to the entire SOP instance
        Bundle? bundle = await _client.SearchAsync<ImagingSelection>(new SearchParams().Add(identifier), cancellationToken);
        if (bundle is null)
            return null;

        // TODO: There could be multiple selections that include the same SOP instance,
        // so we'll throw an error if there are multiple matches.
        return await bundle
            .GetEntriesAsync(_client)
            .Select(x => x.Resource)
            .Cast<ImagingSelection>()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static ImagingSelection UpdateDicomSopInstance(ImagingSelection imagingSelection, DicomDataset dataset)
    {
        imagingSelection.StudyUid = dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID);

        string sopInstanceUid = dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID);
        ImagingSelection.InstanceComponent? instance = imagingSelection.Instance.FirstOrDefault(x => string.Equals(x.Uid, sopInstanceUid, StringComparison.Ordinal));
        if (instance is null)
        {
            instance = new() { Uid = sopInstanceUid };
            imagingSelection.Instance.Add(instance);
        }

        // Update SOP Class
        if (dataset.TryGetSingleValue(DicomTag.SOPClassUID, out string? sopClassUid) && !string.IsNullOrWhiteSpace(sopClassUid))
            instance.SopClass = new Coding("urn:ietf:rfc:3986", $"urn:oid:{sopClassUid}");

        // Update Instance Number
        if (dataset.TryGetSingleValue(DicomTag.InstanceNumber, out int instanceNumber))
            instance.Number = instanceNumber;

        return imagingSelection;
    }
}
