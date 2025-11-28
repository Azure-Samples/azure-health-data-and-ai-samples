using Microsoft.AzureHealth.DataServices.Filters;
using Microsoft.AzureHealth.DataServices.Pipelines;
using Microsoft.Extensions.Logging;
using SMARTCustomOperations.AzureAuth.Configuration;
using SMARTCustomOperations.AzureAuth.Extensions;
using SMARTCustomOperations.AzureAuth.Models;
using SMARTCustomOperations.AzureAuth.Services;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMARTCustomOperations.AzureAuth.Filters
{
    public sealed class ServiceBaseUrlListOutputFilter : IOutputFilter
    {
        private readonly ILogger _logger;
        private readonly AzureAuthOperationsConfig _configuration;
        private readonly string _id;
        private readonly IServiceBaseUrlBundleGeneratorService _bundleGeneratorService;

        public ServiceBaseUrlListOutputFilter(ILogger<ServiceBaseUrlListOutputFilter> logger,
            AzureAuthOperationsConfig configuration, IServiceBaseUrlBundleGeneratorService bundleGeneratorService) 
        {
            _logger = logger;
            _configuration = configuration;
            _id = Guid.NewGuid().ToString();
            _bundleGeneratorService = bundleGeneratorService;
        }

        public event EventHandler<FilterErrorEventArgs>? OnFilterError;

        public string Name => nameof(ServiceBaseUrlListOutputFilter);

		public StatusType ExecutionStatusType => StatusType.Normal;

		public string Id => _id;


        public async Task<OperationContext> ExecuteAsync(OperationContext context)
        {
            if(!context.Request.RequestUri!.LocalPath.Contains("service-base", StringComparison.InvariantCultureIgnoreCase))
            {
                return context;
            }

			_logger?.LogInformation("Entered {Name}", Name);

			try
            {
				Bundle bundleObj = await _bundleGeneratorService.CreateBundle();

				var options = new JsonSerializerOptions
				{
					WriteIndented = true,
					DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
				};

				string jsonString = JsonSerializer.Serialize(bundleObj, options);

				context.ContentString = jsonString;
				context.StatusCode = System.Net.HttpStatusCode.OK;
			}
            catch (Exception ex)
            {
				FilterErrorEventArgs error = new(name: Name, id: Id, fatal: true, error: ex, code: HttpStatusCode.InternalServerError);
				OnFilterError?.Invoke(this, error);
				return context.SetContextErrorBody(error, _configuration.Debug);
			}

            await Task.CompletedTask;
            return context;
        }
    }
}
