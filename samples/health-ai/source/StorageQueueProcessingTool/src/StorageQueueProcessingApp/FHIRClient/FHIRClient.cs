namespace StorageQueueProcessingApp.FHIRClient
{
	public class FHIRClient : IFHIRClient
	{
		private readonly IHttpClientFactory _httpClient;
		private static SemaphoreSlim semaphore = new SemaphoreSlim(10, 15);

		public FHIRClient(IHttpClientFactory httpClient)
		{
			_httpClient = httpClient;
		}
		public async Task<HttpResponseMessage> Send(HttpRequestMessage request, string clientName = "")
		{
			HttpResponseMessage fhirResponse;
			try
			{
				await semaphore.WaitAsync();
				HttpClient client = _httpClient.CreateClient(clientName);
				fhirResponse = await client.SendAsync(request);
			}
			catch
			{
				throw;
			}
			finally { semaphore.Release(); }

			return fhirResponse;
		}
	}
}
