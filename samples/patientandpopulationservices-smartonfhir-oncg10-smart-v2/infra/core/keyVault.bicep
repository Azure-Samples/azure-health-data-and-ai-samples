param vaultName string
param location string
param tenantId string

param writerObjectIds array = []
param readerObjectIds array = []

@description('Specifies whether the key vault is a standard vault or a premium vault.')
@allowed([
  'standard'
  'premium'
])
param skuName string = 'standard'

// Built-in RBAC role definitions for Key Vault
// Key Vault Secrets Officer - Full permissions on secrets
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

// Key Vault Secrets User - Read secrets
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kv 'Microsoft.KeyVault/vaults@2025-05-01' = {
  name: vaultName
  location: location
  properties: {
    tenantId: tenantId
    enableRbacAuthorization: true
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

// Assign Key Vault Secrets Officer role to writer principals
resource writerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for writerId in writerObjectIds: {
  name: guid(kv.id, writerId, keyVaultSecretsOfficerRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsOfficerRoleId)
    principalId: writerId
    principalType: 'User'
  }
}]

// Assign Key Vault Secrets User role to reader principals
resource readerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for readerId in readerObjectIds: {
  name: guid(kv.id, readerId, keyVaultSecretsUserRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: readerId
    principalType: 'ServicePrincipal'
  }
}]
