using System;
using System.ComponentModel.DataAnnotations;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb;

public class DicomClientOptions : FellowOakDicom.Network.Client.DicomClientOptions
{
    public static readonly Uri Audience = new("https://dicom.healthcareapis.azure.com", UriKind.Absolute);

    [Required]
    public string? Name { get; set; }

    [Range(1, int.MaxValue)] // At the time of writing, only 1 and 2 are supported
    public int Version { get; set; }
}
