using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Text;
using WebHookConnector.PostToSubscriber;

namespace WebHookConnector.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WebHookController : ControllerBase
    {

        //private readonly ILogger<WebHookController> _logger;
        private readonly IWebHookCall _webHookCall;
        public WebHookController(IWebHookCall webHookCall)
        {
            _webHookCall = webHookCall;            
        }

        [HttpPost, Route("subscribe")]
        public async Task<HttpResponseMessage> Subscribe([FromBody] WebHookInput body)
        {
            return await _webHookCall.SendAsync(body);
        }

    }
}