@description('Key Vault name. Globally unique, 3-24 chars, alphanumeric and hyphens, must start with a letter.')
param keyVaultName string

@description('Location for the Key Vault.')
param location string

@description('Shared tags applied to the Key Vault.')
param appTags object = {}

@description('Azure AD object IDs (User principals) granted Key Vault Secrets Officer at vault scope. Typically the deployer.')
param writerObjectIds array = []

@description('Azure AD object IDs (Service Principal / Managed Identity) granted Key Vault Secrets User at vault scope. Typically the Function App MI.')
param readerObjectIds array = []

// Built-in Azure RBAC role IDs for Key Vault data plane.
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#key-vault
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'
var secretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: appTags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

resource writerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for objectId in writerObjectIds: {
  name: guid(keyVault.id, objectId, secretsOfficerRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: objectId
    principalType: 'User'
  }
}]

resource readerRoleAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for objectId in readerObjectIds: {
  name: guid(keyVault.id, objectId, secretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsUserRoleId)
    principalId: objectId
    principalType: 'ServicePrincipal'
  }
}]

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
