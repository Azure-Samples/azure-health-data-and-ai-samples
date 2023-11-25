// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal sealed class ResourceTransactionBuilder<T>
{
    private readonly TransactionBuilder _builder;

    public T Resource { get; }

    public ResourceTransactionBuilder(TransactionBuilder builder, T resource)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    public static implicit operator TransactionBuilder(ResourceTransactionBuilder<T> b) => b._builder;
}
