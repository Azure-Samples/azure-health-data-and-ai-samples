using Hl7.Fhir.Model;
using Microsoft.Azure.Functions.Worker.Http;


namespace FHIRPostProcess.PostProcessor
{
    public interface IPostProcess
    {
        
        Task<HttpResponseData> PostProcessResources(HttpRequestData req);

        bool IsEmptyResource(Resource resource);

        bool IsIdAbsentResource(Resource resource);

    }
}
