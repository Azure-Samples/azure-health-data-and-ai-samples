namespace StorageQueueProcessingApp.FHIRClient
{
	public interface IFHIRClient
	{
		Task<HttpResponseMessage> Send(HttpRequestMessage request, string clientName);
	}
}
