using Newtonsoft.Json;
using SMARTCustomOperations.AzureAuth.Models;

namespace SMARTCustomOperations.AzureAuth.Services
{
	public class AuthProvider : IAuthProvider
	{
		private readonly IHttpClientFactory _httpClientFactory;

		public AuthProvider(IHttpClientFactory httpClientFactory)
		{
			_httpClientFactory = httpClientFactory;
		}

		public async Task<OpenIdConfiguration> GetOpenIdConfigurationAsync(string authorityUrl)
		{
			try
			{
				var client = _httpClientFactory.CreateClient();

				var openIdConfigurationUrl = $"{authorityUrl.TrimEnd('/')}/.well-known/openid-configuration";
				var response = await client.GetStringAsync(openIdConfigurationUrl);

				return JsonConvert.DeserializeObject<OpenIdConfiguration>(response)!;
			}
			catch (Exception ex)
			{
				throw new Exception("Error fetching OpenID configuration.", ex);
			}
		}
	}
}
