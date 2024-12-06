// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ConsentSample.FhirOperation
{
    public interface IFhirClient
    {
        Task<HttpResponseMessage> Send(HttpRequestMessage request, Uri fhirUrl, string clientName = "");
    }
}
