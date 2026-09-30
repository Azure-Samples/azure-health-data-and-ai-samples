@description('Name of the existing storage account to grant data-plane access on.')
param storageAccountName string

@description('Object ID of the principal (managed identity / service principal) to grant access to.')
param principalId string

@description('Type of principal being granted access.')
@allowed([
  'ServicePrincipal'
  'User'
])
param principalType string = 'ServicePrincipal'

@description('Storage blob data role to assign.')
@allowed([
  'blobContributor'
  'blobReader'
])
param roleType string

// Built-in Azure RBAC role IDs for Storage blob data plane.
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#storage
var blobContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var blobReaderRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
var roleDefinitionId = roleType == 'blobContributor' ? blobContributorRoleId : blobReaderRoleId

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: storageAccountName
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalId, roleDefinitionId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleDefinitionId)
    principalId: principalId
    principalType: principalType
  }
}
