# FHIR data export Import using resources Id

## Requirements
- The user running the console app must have:
  - **FHIR Data Contributor** access on the FHIR servers (Source and Destination) and
  - **Storage Blob Data Contributor** role on the target storage account/container.

## Quickstart
1. Fill values in `appsettings.json`:
```
{
  "SourceFhirServerUrl": "<<SourceFHIRServerUrl>>",
  "DestinationFhirServerUrl": "<<DestinationFHIRServerUrl>>",
  "History": true,
  "Storage": {
    "AccountName": "<<StorageAccountName>>",
    "Container": "<<StorageContainer>>",
    "BlobNamePrefix": "imports/",
    "BlobName": "export.ndjson"
  },
  "PreferRespondAsync": true
}
```
2. Ensure your environment has a managed identity `az login` for local dev.
3. Create `resourceIds.txt` with resources (one per line):
```
Patient/123
Observation/abc
ExplanationOfBenefit/eob001
```
4. Build and run:
    - Go the ExportImportResources/ExportImportUility in the sample and run below command to execute the sample after setting the appsettings.json and resourceIds.txt files
    ```bash
    dotnet build
    dotnet run
    ```
5. After the sample has completed execution, an import content location will be displayed in the terminal.
    - Use this import content location to check the status of the $import operation.