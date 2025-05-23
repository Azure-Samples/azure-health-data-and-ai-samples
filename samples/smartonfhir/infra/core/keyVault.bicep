param vaultName string
param location string
param tenantId string
param enableVNetSupport bool
param smartonfhirwithb2c bool

param writerObjectIds array = []
param readerObjectIds array = []

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param skuName string = enableVNetSupport ? 'premium' : 'standard'

var readerAccessPolicy = [for readerId in readerObjectIds: {
  objectId: readerId
  tenantId: tenantId
  permissions: {
    secrets: ['list', 'get']
  }
}]

var writerAccessPolicy = [for writerId in writerObjectIds: {
  objectId: writerId
  tenantId: tenantId
  permissions: {
    secrets: ['all', 'purge']
  }
}]

var comboAccessPolicy = union(readerAccessPolicy, writerAccessPolicy)

resource kv 'Microsoft.KeyVault/vaults@2021-11-01-preview' = if (smartonfhirwithb2c) {
  name: vaultName
  location: location
  properties: {
    tenantId: tenantId
    accessPolicies: comboAccessPolicy
    sku: {
      name: skuName
      family: 'A'
    }
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}
