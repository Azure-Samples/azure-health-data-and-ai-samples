using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FhirBlaze.SharedComponents.Services
{
	public interface IHttpHelperMethods
	{
		Task<HttpResponseMessage> PostAsync(string uri,string content, bool isFHIROperation);
		Task<HttpResponseMessage> PutAsync(string uri, string content);
		Task<HttpResponseMessage> DeleteAsync(string uri);
	}
}
