// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using Hl7.Fhir.Model;

namespace Azure.Health.Data.Dicom.Cast.Fhir;

internal class FhirObservationCodes
{
    public static readonly CodeableConcept RadiationExposure = new("http://loinc.org", "73569-6", "Radiation exposure and protection information");
    public static readonly CodeableConcept Accession = new("http://terminology.hl7.org/CodeSystem/v2-0203", "ACSN");
    public static readonly CodeableConcept IrradiationEvent = new("http://dicom.nema.org/resources/ontology/DCM", "113852", "Irradiation Event");
}
