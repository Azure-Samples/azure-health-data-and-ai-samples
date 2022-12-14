namespace WebHookConnector.PostToSubscriber
{
    public interface IWebHookCall
    {
        Task<HttpResponseMessage> SendAsync(WebHookInput httpRequestMessage);
    }
}
