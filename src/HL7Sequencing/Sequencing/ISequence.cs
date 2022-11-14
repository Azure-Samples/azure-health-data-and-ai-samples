using Microsoft.Azure.Functions.Worker.Http;

namespace HL7Sequencing.Sequencing
{
    public interface ISequence
    {
        Task<HttpResponseData> GetSequencListAsync(HttpRequestData request);
    }
}
