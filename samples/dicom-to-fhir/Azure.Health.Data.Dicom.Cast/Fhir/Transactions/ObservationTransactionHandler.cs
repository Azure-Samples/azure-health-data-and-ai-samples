// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FellowOakDicom;
using FellowOakDicom.StructuredReport;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal class ObservationTransactionHandler(FhirClient client, ILogger<ObservationTransactionHandler> logger)
{
    private readonly FhirClient _client = client ?? throw new ArgumentNullException(nameof(client));
    private readonly ILogger<ObservationTransactionHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<Patient> AddOrUpdateObservationAsync(
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

        if (dataset.TryGetSequence(DicomTag.ConceptNameCodeSequence, out DicomSequence codes) && codes?.Items.Count > 0)
        {
            DicomCodeItem code = new(codes);

            if (StructuredReportCodes.IrradiationEvents.Contains(code) && TryCreateIrradiationEvent(dataset, patientReference, identifier, out Observation irradiationEvent))
                yield return irradiationEvent;

            if (StructuredReportCodes.DoseSummaryReportCodes.Contains(code))
                yield return CreateDoseSummary(dataset, imagingStudyReference, patientReference, identifier);
        }
    }
}
