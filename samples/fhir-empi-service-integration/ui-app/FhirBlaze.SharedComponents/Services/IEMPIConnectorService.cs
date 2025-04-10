using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public interface IEMPIConnectorService
	{
		Task<HttpResponseMessage> GetPatientMatchAsync(string requestBody);
	}
}
