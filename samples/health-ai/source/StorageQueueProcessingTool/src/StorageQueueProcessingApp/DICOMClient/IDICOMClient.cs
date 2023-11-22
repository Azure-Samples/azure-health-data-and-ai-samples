using Microsoft.Health.Dicom.Client;

namespace StorageQueueProcessingApp.DICOMClient
{
	public interface IDICOMClient
	{
		Task<DicomWebResponse> Store(Stream dicomStream);
	}
}
