using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
    public interface IAPIMService
    {
        Task<HttpResponseMessage> GetPatientObservations(string firstName, string lastName);

        Task<HttpResponseMessage> GetLookUpCode(string codeValue, string codeSystem);

        Task<HttpResponseMessage> TranslateCode(string sourceCode, string sourceCodeSystem, string targetCodeSystem);

        Task<HttpResponseMessage> SaveObservation(string id, string observationJson);

        Task<HttpResponseMessage> ResetObservations(string content);
        Task<HttpResponseMessage> BatchTranslateCode(string content);
    }
}
