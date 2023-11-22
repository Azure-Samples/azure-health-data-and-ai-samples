namespace StorageQueueProcessingApp.FHIRClient
{
	public class FHIRClient : IFHIRClient
	{
		private readonly IHttpClientFactory _httpClient;

		public FHIRClient(IHttpClientFactory httpClient)
		{
			_httpClient = httpClient;
		}
		public async Task<HttpResponseMessage> Send(HttpRequestMessage request, string clientName = "")
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
