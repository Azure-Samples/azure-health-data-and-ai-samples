// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using FellowOakDicom;

namespace Azure.Health.Data.Dicom.Cast;

internal class DicomSopInstanceEvent
{
    public InstanceIdentifiers Target { get; }

    public InstanceAction Action { get; }

    public DicomDataset Dataset { get; }

    public DicomSopInstanceEvent(InstanceIdentifiers target, InstanceAction action, DicomDataset dataset)
    {
        Target = target;
        Action = action;
        Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
    }
}
