param storageAccountName string
param filesystemName string
param synapseworkspaceName string

param sqlAdministratorLogin string
param location string
param appTags object = {}

@secure()
param sqlAdministratorLoginPassword string
param allowAllConnections bool

@description('Azure Data lake storage account')
resource StorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    isHnsEnabled: true
  }
  tags: appTags
}

@description('Azure Data lake Filesystem')
resource filesystem 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-09-01' = {
  name: '${storageAccountName}/default/${filesystemName}'
  dependsOn: [
    StorageAccount
  ]
}

resource synapseworkspace 'Microsoft.Synapse/workspaces@2020-12-01' = {
  name: synapseworkspaceName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    defaultDataLakeStorage: {
      accountUrl: 'https://${StorageAccount.name}.dfs.core.windows.net'
      filesystem: filesystemName
    }
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorLoginPassword: sqlAdministratorLoginPassword
  }
  tags: appTags
  dependsOn: [
    StorageAccount
    filesystem
  ]
}

resource name_allowAll 'Microsoft.Synapse/workspaces/firewallrules@2021-06-01-preview' = if (allowAllConnections) {
  parent: synapseworkspace
  name: 'allowAll'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '255.255.255.255'
  }
}

output synapseworkspacename string = synapseworkspace.name
output dataLakeName string = StorageAccount.name
