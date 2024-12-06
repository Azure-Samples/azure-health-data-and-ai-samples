using ClientApplication.Configuration;
using Grpc.Net.Client.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ClientApplication
{
    public class Clientapp
    {
        private readonly ILogger<Clientapp> _logger;
        private readonly config _options;

        public Clientapp(ILogger<Clientapp> logger, config options)
        {
            _logger = logger;
            _options = options;
        }

        [Function("clientapp")]
        public async Task<IActionResult> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post", "put", "delete", Route = "{*Resource}")] HttpRequest req)
        {   

            var Urlnew = _options.FhirServerUrl;

            // Uri uri = new Uri(req.Path);
            string input = req.Path;

            var fhirServelUrl = Urlnew + input.TrimStart('/');
            var client = new HttpClient();

            string method = req.Method;
            HttpMethod httpMethod;
            if (method == HttpMethods.Get)
            {
                httpMethod = HttpMethod.Get;
            }
            else if (method == HttpMethods.Post)
            {
                httpMethod = HttpMethod.Post;
            }
            else if (method == HttpMethods.Put)
            {
                httpMethod = HttpMethod.Put;
            }
            else
            {
                httpMethod = HttpMethod.Delete;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Define claims
            var claims = new[]
            {
                new Claim("role", _options.role),
                new Claim("role-id", _options.role_id)
            };

            // Create token
            var token = new JwtSecurityToken(
                issuer: _options.issuer,
                audience: _options.audience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            string tk = new JwtSecurityTokenHandler().WriteToken(token);

            var request = new HttpRequestMessage(httpMethod, fhirServelUrl);

            if (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            {
                var content = await new StreamReader(req.Body).ReadToEndAsync();
                request.Content = new StringContent(content, System.Text.Encoding.UTF8, req.ContentType ?? "application/json");
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tk);
            request.Headers.Add("fhirproxy-roles", _options.fhirproxy_roles);

            var response = await client.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();
            return new OkObjectResult(result);

        }
    }
}
