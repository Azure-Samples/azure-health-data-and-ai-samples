// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ConsentSample.Processors
{
    public interface IFhirProcessor
    {
        Task<HttpResponseMessage> CallProcess(HttpMethod method, string requestContent, Uri baseUri, string queryString, string endpoint);

    }
}
