using Microsoft.Health.Dicom.Client;

namespace StorageQueueProcessingApp.Processors
{
	public interface IDICOMProcessor
	{
		Task<DicomWebResponse> CallDICOMMethod(Stream dicomStream);
	}
}
