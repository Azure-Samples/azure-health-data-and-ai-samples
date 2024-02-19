using SMARTCustomOperations.AzureAuth.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMARTCustomOperations.AzureAuth.Services
{
	public interface IAuthProvider
	{
		Task<OpenIdConfiguration> GetOpenIdConfigurationAsync(string authorityUrl);
	}
}
