// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

namespace ConsentSample.FhirOperation
{
    public class FhirClient : IFhirClient
    {
        private readonly IHttpClientFactory _httpClient;

        public FhirClient(IHttpClientFactory httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<HttpResponseMessage> Send(HttpRequestMessage request, Uri fhirUrl, string clientName = "")
        {
            HttpResponseMessage fhirResponse;
            try
            {
                HttpClient client = _httpClient.CreateClient(clientName);
                fhirResponse = await client.SendAsync(request);
            }
            catch
            {
                throw;
            }

            return fhirResponse;
        }
    }
}
