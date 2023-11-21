// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FellowOakDicom.StructuredReport;
using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class DicomStructuredReportExtensions
{
    public static bool TryGetDataType(this DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out DataType? dataType)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(codeItem);

        if (DicomCodeItemMapping.TryGetValue(codeItem, out ComponentType type))
        {
            return type switch
            {
                ComponentType.Quantity => report.TryGetQuantityData(codeItem, out dataType),
                ComponentType.Text => report.TryGetTextData(codeItem, out dataType),
                ComponentType.Code => report.TryGetCodeData(codeItem, out dataType),
                ComponentType.Integer => report.TryGetIntegerData(codeItem, out dataType),
                _ => throw new InvalidOperationException("Unknown component type.")
            };
        }

        dataType = default;
        return false;
    }

    private static bool TryGetQuantityData(this DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out DataType? dataType)
    {
        DicomMeasuredValue? measurement = report.Get<DicomMeasuredValue?>(codeItem, default); // The 2nd argument is unused
        if (measurement is not null)
        {
            dataType = new Quantity(measurement.Value, measurement.Code.Value);
            return true;
        }

        dataType = default;
        return false;
    }

    private static bool TryGetTextData(this DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out DataType? dataType)
    {
        string? text = report.Get<string?>(codeItem, default); // The 2nd argument is unused
        if (!string.IsNullOrEmpty(text))
        {
            dataType = new FhirString(text);
            return true;
        }

        dataType = default;
        return false;
    }

    private static bool TryGetCodeData(this DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out DataType? dataType)
    {
        DicomCodeItem? code = report.Get<DicomCodeItem?>(codeItem, default); // The 2nd argument is unused
        if (code is not null)
        {
            dataType = new CodeableConcept(codeItem.GetSystem(), code.Value, code.Meaning);
            return true;
        }

        dataType = default;
        return false;
    }

    private static bool TryGetIntegerData(this DicomStructuredReport report, DicomCodeItem codeItem, [NotNullWhen(true)] out DataType? dataType)
    {
        int value = report.Get<int>(codeItem, default); // The 2nd argument is unused
        if (value != 0)
        {
            dataType = new Integer(value);
            return true;
        }

        dataType = default;
        return false;
    }

    private static readonly ImmutableDictionary<DicomCodeItem, ComponentType> DicomCodeItemMapping = ImmutableDictionary.CreateRange(
        new KeyValuePair<DicomCodeItem, ComponentType>[]
        {
            new(StructuredReportCodes.AccumulatedAverageGlandularDose, ComponentType.Quantity),
            new(StructuredReportCodes.AcquisitionDoseAreaProductTotal, ComponentType.Quantity),
            new(StructuredReportCodes.AdministeredActivity, ComponentType.Quantity),
            new(StructuredReportCodes.CtdIwPhantomType, ComponentType.Code),
            new(StructuredReportCodes.CtDoseLengthProductTotal, ComponentType.Quantity),
            new(StructuredReportCodes.Dlp, ComponentType.Quantity),
            new(StructuredReportCodes.DoseAreaProductTotal, ComponentType.Quantity),
            new(StructuredReportCodes.DoseRpTotal, ComponentType.Quantity),
            new(StructuredReportCodes.EntranceExposureAtRp, ComponentType.Quantity),
            new(StructuredReportCodes.FluoroDoseAreaProductTotal, ComponentType.Quantity),
            new(StructuredReportCodes.MeanCtdIvol, ComponentType.Quantity),
            new(StructuredReportCodes.Radionuclide, ComponentType.Text),
            new(StructuredReportCodes.RadiopharmaceuticalAgent, ComponentType.Text),
            new(StructuredReportCodes.RadiopharmaceuticalVolume, ComponentType.Quantity),
            new(StructuredReportCodes.RouteOfAdministration, ComponentType.Code),
            new(StructuredReportCodes.TotalFluoroTime, ComponentType.Quantity),
            new(StructuredReportCodes.TotalNumberOfIrradiationEvents, ComponentType.Integer),
            new(StructuredReportCodes.TotalNumberOfRadiographicFrames, ComponentType.Integer),
        });

    private enum ComponentType
    {
        Quantity,
        Text,
        Code,
        Integer,
    }
}
