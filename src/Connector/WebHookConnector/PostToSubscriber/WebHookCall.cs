using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Text;

namespace WebHookConnector.PostToSubscriber
{
    public class WebHookCall : IWebHookCall
    {

        public WebHookCall(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private readonly HttpClient _httpClient;

        public async Task<HttpResponseMessage> SendAsync(WebHookInput body)
        {
            try
            {
                byte[] data = Convert.FromBase64String(body.Requestbody);
                string decodedString = Encoding.UTF8.GetString(data);
                var content = new StringContent(decodedString, Encoding.UTF8, "application/json");
                var httpResponseMessage = await _httpClient.PostAsync("", content);
                if (httpResponseMessage.StatusCode == HttpStatusCode.OK)
                {
                    var callBackResponse = await _httpClient.PostAsync("https://wf-hl7ingestpipeline.azurewebsites.net:443/api/wf-hl7Fhirpipeline/triggers/manual/invoke?api-version=2022-05-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=jxxmqMWeYhO7bY8JZpXMXtJCWXuFl3aPX00mcfydrB4", httpResponseMessage.Content);
                    return await Task.FromResult(callBackResponse);
                }
                return await Task.FromResult(httpResponseMessage);

            }
            catch (Exception ex)
            {
                HttpResponseMessage httpResponseMessage = new()
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = new StringContent(ex.Message, Encoding.UTF8, "application/json")
                };
                return await Task.FromResult(httpResponseMessage);
            }

        }
    }
}
