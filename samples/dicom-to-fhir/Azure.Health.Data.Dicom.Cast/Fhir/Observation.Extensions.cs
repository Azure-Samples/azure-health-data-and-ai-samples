// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FellowOakDicom.StructuredReport;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class ObservationExtensions
{
    public static void AddComponents(this Observation observation, DicomStructuredReport report, params DicomCodeItem[] codeItems)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(codeItems);

        foreach (DicomCodeItem codeItem in codeItems)
        {
            if (TryGetObservationComponent(report, codeItem, out Observation.ComponentComponent? component))
                observation.Component.Add(component);
        }

        foreach (DicomStructuredReport childReport in report.Children().Select(x => new DicomStructuredReport(x.Dataset)))
            AddComponents(observation, childReport, codeItems);
    }

    private static bool TryGetObservationComponent(DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out Observation.ComponentComponent? component)
    {
        if (report.TryGetDataType(codeItem, out DataType? dataType))
        {
            component = new Observation.ComponentComponent
            {
                Code = new CodeableConcept(codeItem.GetSystem(), codeItem.Value, codeItem.Meaning),
                Value = dataType
            };

            return true;
        }

        component = default;
        return false;
    }
}
