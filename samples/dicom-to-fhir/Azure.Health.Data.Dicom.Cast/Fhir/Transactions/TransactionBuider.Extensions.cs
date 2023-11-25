// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal static class TransactionBuider
{
    public static ResourceTransactionBuilder<T> ForResource<T>(this TransactionBuilder builder, T resource)
        => new(builder, resource);
}
