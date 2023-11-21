// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Azure.Health.Data.Dicom.Cast;

internal readonly struct InstanceIdentifiers : IEquatable<InstanceIdentifiers>
{
    public string StudyInstanceUid { get; }

    public string SeriesInstanceUid { get; }

    public string SopInstanceUid { get; }

    public InstanceIdentifiers(string studyInstanceUid, string seriesInstanceUid, string sopInstanceUid)
    {
        ArgumentException.ThrowIfNullOrEmpty(studyInstanceUid);
        ArgumentException.ThrowIfNullOrEmpty(seriesInstanceUid);
        ArgumentException.ThrowIfNullOrEmpty(sopInstanceUid);

        StudyInstanceUid = studyInstanceUid;
        SeriesInstanceUid = seriesInstanceUid;
        SopInstanceUid = sopInstanceUid;
    }

    public bool Equals(InstanceIdentifiers other)
    {
        return StudyInstanceUid == other.StudyInstanceUid
            && SeriesInstanceUid == other.SeriesInstanceUid
            && SopInstanceUid == other.SopInstanceUid;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is InstanceIdentifiers other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(StudyInstanceUid, SeriesInstanceUid, SopInstanceUid);

    public static bool operator ==(InstanceIdentifiers left, InstanceIdentifiers right)
        => left.Equals(right);

    public static bool operator !=(InstanceIdentifiers left, InstanceIdentifiers right)
        => !left.Equals(right);
}
