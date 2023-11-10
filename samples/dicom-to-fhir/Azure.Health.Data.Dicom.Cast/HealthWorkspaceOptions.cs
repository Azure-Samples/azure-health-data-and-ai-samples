using Azure.Health.Data.Dicom.Cast.DicomWeb;
using Azure.Health.Data.Dicom.Cast.Fhir;
using System;
using System.ComponentModel.DataAnnotations;

namespace Azure.Health.Data.Dicom.Cast;

public class HealthWorkspaceOptions
{
    public const string SectionName = "Workspace";

    [Required]
    public string? Name { get; set; }

    [Required]
    public FhirClientOptions? Fhir { get; set; }

    [Required]
    public DicomClientOptions? Dicom { get; set; }

    public string? Credential { get; set; }

    public string? ClientId { get; set; }

    public Uri DicomServiceUri => new($"https://{Name}-{Dicom?.Name}.dicom.azurehealthcareapis.com", UriKind.Absolute);

    public Uri FhirServiceUri => new($"https://{Name}-{Fhir?.Name}.fhir.azurehealthcareapis.com", UriKind.Absolute);
}
