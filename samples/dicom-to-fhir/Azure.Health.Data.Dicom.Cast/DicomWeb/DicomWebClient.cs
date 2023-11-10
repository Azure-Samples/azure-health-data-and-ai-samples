using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Health.Data.Dicom.Cast.DicomWeb
{
    internal class DicomWebClient
    {
        public DicomWebClient(HttpClient httpClient, Uri baseUri)
        {
            HttpClient = httpClient;
            BaseUri = baseUri;
        }
    }
}
