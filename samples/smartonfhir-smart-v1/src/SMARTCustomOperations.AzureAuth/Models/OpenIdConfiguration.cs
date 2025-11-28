using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMARTCustomOperations.AzureAuth.Models
{
	public class OpenIdConfiguration
	{
		[JsonProperty("issuer")]
		public string? Issuer { get; set; }

		[JsonProperty("authorization_endpoint")]
		public string? AuthorizationEndpoint { get; set; }

		[JsonProperty("token_endpoint")]
		public string? TokenEndpoint { get; set; }
	}
}
