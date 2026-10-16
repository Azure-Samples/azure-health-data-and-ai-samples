@description('Name for the dedicated export storage account (3-24 chars, lowercase alphanumeric).')
@minLength(3)
@maxLength(24)
param exportStorageAccountName string

@description('Location for the export storage account.')
param location string

@description('Shared tags applied to the export storage account.')
param appTags object = {}

// Dedicated storage account for FHIR bulk-export output (NDJSON). Kept separate from the
// Function App runtime storage so exported PHI is isolated.
resource exportStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: exportStorageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
  tags: appTags
}

output exportStorageAccountName string = exportStorageAccount.name
output exportStorageBlobUri string = exportStorageAccount.properties.primaryEndpoints.blob
