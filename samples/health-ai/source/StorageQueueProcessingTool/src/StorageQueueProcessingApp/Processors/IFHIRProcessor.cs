namespace StorageQueueProcessingApp.Processors
{
	public interface IFHIRProcessor
	{
		Task<HttpResponseMessage> CallFHIRMethod(string body, HttpMethod method, string endpoint);
	}
}
