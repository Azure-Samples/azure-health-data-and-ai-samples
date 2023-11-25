// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal sealed class ResourceTransactionBuilder<T>(TransactionBuilder builder, T resource)
{
    private readonly TransactionBuilder _builder = builder ?? throw new ArgumentNullException(nameof(builder));

    public T Resource { get; } = resource ?? throw new ArgumentNullException(nameof(resource));

    public static implicit operator TransactionBuilder(ResourceTransactionBuilder<T> b) => b._builder;
}
