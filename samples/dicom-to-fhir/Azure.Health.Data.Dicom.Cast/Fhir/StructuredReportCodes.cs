// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Immutable;
using FellowOakDicom.StructuredReport;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal static class StructuredReportCodes
{
    //------------------------------------------------------------
    // Report codes
    // - When you encounter these codes in a structured report, it means to create a new "Does Summary" Observation
    //------------------------------------------------------------
    public static readonly DicomCodeItem RadiopharmaceuticalRadiationDoseReport = new("113500", CodeSchemes.Dcm, "Radiopharmaceutical Radiation Dose Report");
    public static readonly DicomCodeItem XRayRadiationDoseReport = new("113701", CodeSchemes.Dcm, "X-Ray Radiation Dose Report");

    //------------------------------------------------------------
    // Irradiation Event Codes
    // - When you encounter these code in a structured report, it means to create a new "Irradiation Event" Observation
    //------------------------------------------------------------
    public static readonly DicomCodeItem IrradiationEventXRayData = new("113706", CodeSchemes.Dcm, "Irradiation Event X-Ray Data");
    public static readonly DicomCodeItem CtAcquisition = new("113819", CodeSchemes.Dcm, "CT Acquisition");
    public static readonly DicomCodeItem RadiopharmaceuticalAdministration = new("113502", CodeSchemes.Dcm, "Radiopharmaceutical Administration");

    //------------------------------------------------------------
    // Dicom Codes (attribute)
    // - These are report values which map to non component observation attributes.
    //------------------------------------------------------------
    public static readonly DicomCodeItem IrradiationAuthorizingPerson = new("113850", CodeSchemes.Dcm, "Irradiation Authorizing");
    public static readonly DicomCodeItem PregnancyObservation = new("364320009", CodeSchemes.Sct, "Pregnancy observable");
    public static readonly DicomCodeItem IndicationObservation = new("18785-6", CodeSchemes.Ln, "Indications for Procedure");
    public static readonly DicomCodeItem IrradiatingDevice = new("113859", CodeSchemes.Dcm, "Irradiating Device");

    public static readonly DicomCodeItem IrradiationEventUid = new("113769", CodeSchemes.Dcm, "Irradiation Event UID");
    public static readonly DicomCodeItem StudyInstanceUid = new("110180", CodeSchemes.Dcm, "Study Instance UID");
    public static readonly DicomCodeItem AccessionNumber = new("121022", CodeSchemes.Dcm, "Accession Number");
    public static readonly DicomCodeItem StartOfXrayIrradiation = new("113809", CodeSchemes.Dcm, "Start of X-Ray Irradiation”)");

    //------------------------------------------------------------
    // Dicom codes (component)
    // - These are report values which map to Observation.component values
    //------------------------------------------------------------
    // Study
    public static readonly DicomCodeItem DoseRpTotal = new("113725", CodeSchemes.Dcm, "Dose (RP) Total");
    public static readonly DicomCodeItem EntranceExposureAtRp = new("111636", CodeSchemes.Dcm, "Entrance Exposure at RP");
    public static readonly DicomCodeItem AccumulatedAverageGlandularDose = new("111637", CodeSchemes.Dcm, "Accumulated Average Glandular Dose");
    public static readonly DicomCodeItem DoseAreaProductTotal = new("113722", CodeSchemes.Dcm, "Dose Area Product Total");
    public static readonly DicomCodeItem FluoroDoseAreaProductTotal = new("113726", CodeSchemes.Dcm, "Fluoro Dose Area Product Total");
    public static readonly DicomCodeItem AcquisitionDoseAreaProductTotal = new("113727", CodeSchemes.Dcm, "Acquisition Dose Area Product Total");
    public static readonly DicomCodeItem TotalFluoroTime = new("113730", CodeSchemes.Dcm, "Total Fluoro Time");
    public static readonly DicomCodeItem TotalNumberOfRadiographicFrames = new("113731", CodeSchemes.Dcm, "Total Number of Radiographic Frames");
    public static readonly DicomCodeItem AdministeredActivity = new("113507", CodeSchemes.Dcm, "Administered activity");
    public static readonly DicomCodeItem CtDoseLengthProductTotal = new("113813", CodeSchemes.Dcm, "CT Dose Length Product Total");
    public static readonly DicomCodeItem TotalNumberOfIrradiationEvents = new("113812", CodeSchemes.Dcm, "");
    public static readonly DicomCodeItem RadiopharmaceuticalAgent = new("349358000", CodeSchemes.Sct, "Radiopharmaceutical agent");
    public static readonly DicomCodeItem Radionuclide = new("89457008", CodeSchemes.Sct, "Radionuclide");
    public static readonly DicomCodeItem RadiopharmaceuticalVolume = new("123005", CodeSchemes.Dcm, "Radiopharmaceutical Volume");
    public static readonly DicomCodeItem RouteOfAdministration = new("410675002", CodeSchemes.Sct, "Route of administration");

    // (Ir)radiation Event
    // uses MeanCtdIvol as well
    public static readonly DicomCodeItem MeanCtdIvol = new("113830", CodeSchemes.Dcm, "Mean CTDIvol");
    public static readonly DicomCodeItem Dlp = new("113838", CodeSchemes.Dcm, "DLP");
    public static readonly DicomCodeItem TargetRegion = new("123014", CodeSchemes.Dcm, "Target Region");
    public static readonly DicomCodeItem CtdIwPhantomType = new("113835", CodeSchemes.Dcm, "CTDIw Phantom Type");

    public static readonly ImmutableHashSet<DicomCodeItem> IrradiationEvents = [IrradiationEventXRayData, CtAcquisition, RadiopharmaceuticalAdministration];
}
