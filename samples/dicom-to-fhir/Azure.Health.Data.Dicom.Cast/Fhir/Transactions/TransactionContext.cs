// Copyright © Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Hl7.Fhir.Rest;

namespace Azure.Health.Data.Dicom.Cast.Fhir.Transactions;

internal abstract class TransactionContext
{
    public TransactionBuilder Builder { get; }

    protected TransactionContext(TransactionBuilder builder)
        => Builder = builder ?? throw new ArgumentNullException(nameof(builder));
}
