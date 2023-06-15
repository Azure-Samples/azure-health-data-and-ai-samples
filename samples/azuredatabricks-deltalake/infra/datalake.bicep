@description('Azure Storage ADLS Gen 2 accout name')
param name string

param location string

@description('Storage Account type')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param storageAccountType string = 'Standard_LRS'

@description('The name of the container to store job and data.')
param lakeContainerName string = 'fhir'

param importContainerName string = 'import'

param tags object = {}

@description('Storage account for our datalake sink of FHIR data.')
resource storage_account 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: storageAccountType
  }
  properties: {
    isHnsEnabled: true
    supportsHttpsTrafficOnly: true
  }

  kind: 'StorageV2'

  resource blob_service 'blobServices' existing = {
    name: 'default'
  }

  resource blobService 'blobServices' = {
    name: 'default'

    resource lakeContainer 'containers' = {
      name: lakeContainerName
    }

    resource importContainer 'containers' = {
      name: importContainerName
    }
  }
}

output storage_account_name string = storage_account.name
output storage_account_id string = storage_account.id
