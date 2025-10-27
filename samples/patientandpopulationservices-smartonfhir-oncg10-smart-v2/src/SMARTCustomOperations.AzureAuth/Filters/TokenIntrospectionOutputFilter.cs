using Microsoft.AzureHealth.DataServices.Clients.Headers;
using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Factories;
using SMARTCustomOperations.AzureAuth.Models;
using System.Collections.Specialized;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class TokenIntrospectionOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id;
        private readonly ITokenIntrospectionServiceFactory _introspectionServiceFactory;

        public TokenIntrospectionOutputFilter(ILogger<TokenIntrospectionOutputFilter> logger, AzureAuthOperationsConfig configuration, ITokenIntrospectionServiceFactory introspectionServiceFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
            _introspectionServiceFactory = introspectionServiceFactory;
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(TokenIntrospectionOutputFilter);

        public StatusType ExecutionStatusType => StatusType.Normal;

        public string Id => _id;

        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            if(!context.Request.RequestUri!.LocalPath.Contains("introspection", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

            _logger?.LogInformation("Entered {Name}", Name);

            if (IsRequestUrlFormEncoded(context))
            {
                FilterErrorEventArgs error = new FilterErrorEventArgs(name: Name, id: Id, fatal: true, error: new ArgumentException("Content Type must be application/x-www-form-urlencoded"), code: HttpStatusCode.BadRequest);
                OnFilterError?.Invoke(this, error);
                return context.SetContextErrorBody(error, _configuration.Debug);
            }

            context.Request!.Content!.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

            // Read the request body
            NameValueCollection requestData = await context.Request.Content.ReadAsFormDataAsync();

            try
            {
                var token = requestData.GetValues("token")!.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(token))
                {
                    context.ContentString = "Missing token";
                    context.StatusCode = HttpStatusCode.BadRequest;
                    return context;
                }

                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                var issuer = jwt.Issuer;

                var service = _introspectionServiceFactory.GetService(issuer);
                var result = await service.IntrospectAsync(token);

                context.StatusCode = HttpStatusCode.OK;
                context.Headers.Add(new HeaderNameValuePair("Content-Type", "application/json", CustomHeaderType.ResponseStatic));
                context.ContentString = JsonSerializer.Serialize(result);
                return context;
            }
            catch (Exception ex)
            {
                FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
                OnFilterError?.Invoke(this, error);
                context.StatusCode = HttpStatusCode.OK;
                context.ContentString = JsonSerializer.Serialize(new TokenIntrospectionResult { Active = false });
                return context;
            }
        }

        private bool IsRequestUrlFormEncoded(OperationContext context)
        {
            return context.Request.Content == null ||
                !context.Request.Content.Headers.GetValues("Content-Type")
                .Any(x => string.Equals(x.Split(";").FirstOrDefault(), "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase));
        }

    }
}
