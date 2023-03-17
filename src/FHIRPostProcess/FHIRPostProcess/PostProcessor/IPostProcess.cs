using FHIRPostProcess.Model;
using Hl7.Fhir.Model;
using Microsoft.Azure.Functions.Worker.Http;
using System.Runtime.CompilerServices;

namespace FHIRPostProcess.PostProcessor
{
    public interface IPostProcess
    {
        Task<string> PostProcessResources(OrchestrationInput orchestrationInput);

        bool IsEmptyResource(Resource resource);

        bool IsIdAbsentResource(Resource resource);

        Task<string> GetHl7FilesList(string hl7ArrayFileName);

    }
}
