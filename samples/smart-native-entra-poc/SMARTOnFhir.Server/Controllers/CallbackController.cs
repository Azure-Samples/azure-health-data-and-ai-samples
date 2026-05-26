using Microsoft.AspNetCore.Mvc;

namespace SMARTOnFhir.Server.Controllers
{
    [ApiController]
    public class CallbackController : ControllerBase
    {
        [HttpGet("/callback")]
        public IActionResult Callback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error,
            [FromQuery] string? error_description,
            [FromQuery] string? session_state)
        {
            if (!string.IsNullOrEmpty(error))
            {
                return Content($@"
<html><body>
<h2>Authorization Error</h2>
<p><b>Error:</b> {System.Web.HttpUtility.HtmlEncode(error)}</p>
<p><b>Description:</b> {System.Web.HttpUtility.HtmlEncode(error_description)}</p>
</body></html>", "text/html");
            }

            if (string.IsNullOrEmpty(code))
            {
                return BadRequest("No authorization code received");
            }

            var host = $"{Request.Scheme}://{Request.Host}";

            return Content($@"
<html><body>
<h2>Authorization Code Received</h2>
<p><b>State:</b> {System.Web.HttpUtility.HtmlEncode(state)}</p>
<p><b>Code:</b> <code style=""word-break:break-all"">{System.Web.HttpUtility.HtmlEncode(code[..Math.Min(20, code.Length)])}...</code></p>
<hr/>
<h3>Exchange for Token</h3>
<p>POST to <code>{host}/auth/token</code> with:</p>
<pre>
grant_type=authorization_code
code={System.Web.HttpUtility.HtmlEncode(code[..Math.Min(20, code.Length)])}...
client_id=&lt;your-client-id&gt;
redirect_uri={System.Web.HttpUtility.HtmlEncode(host)}/callback
</pre>
<hr/>
<form method=""POST"" action=""{host}/auth/token"" enctype=""application/x-www-form-urlencoded"">
  <h3>Quick Token Exchange</h3>
  <input type=""hidden"" name=""grant_type"" value=""authorization_code"" />
  <input type=""hidden"" name=""code"" value=""{System.Web.HttpUtility.HtmlAttributeEncode(code)}"" />
  <input type=""hidden"" name=""redirect_uri"" value=""{host}/callback"" />
  <label>Client ID: <input name=""client_id"" size=""40"" value="""" /></label><br/><br/>
  <label>Client Secret (optional): <input name=""client_secret"" size=""40"" /></label><br/><br/>
  <label>Code Verifier (PKCE, optional): <input name=""code_verifier"" size=""40"" /></label><br/><br/>
  <button type=""submit"">Exchange Code for Token</button>
</form>
</body></html>", "text/html");
        }
    }
}
