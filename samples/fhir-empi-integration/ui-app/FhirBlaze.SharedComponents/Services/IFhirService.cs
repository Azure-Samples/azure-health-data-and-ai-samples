using System.Net.Http;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public interface IFhirService
    {
        #region Patient
        Task<HttpResponseMessage> CreatePatientsAsync(string patient);
        Task<HttpResponseMessage> UpdatePatientAsync(string patientId, string patient);
        Task<HttpResponseMessage> DeletePatientAsync(string patientId);
        #endregion
    }
}