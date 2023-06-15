param location string
param managedIdentityName string

// Owner role needed for databricks
var ownerRoleDefId = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635'
var storageDataContributorRole = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var fhirDataImporterRole = '4465e953-8ced-4406-a58e-0f6e3f3b530b'
// var fhirDataContributorRole = '5a1fc7df-4bf1-4951-a576-89034ee01acd'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: managedIdentityName
  location: location
}

resource ownerRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name:  guid(ownerRoleDefId,resourceGroup().id)
  scope: resourceGroup()
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', ownerRoleDefId)
  }
}

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name:  guid(storageDataContributorRole,resourceGroup().id)
  scope: resourceGroup()
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageDataContributorRole)
  }
}

resource fhirImporterRoleAssigment 'Microsoft.Authorization/roleAssignments@2020-08-01-preview' = {
  name:  guid(fhirDataImporterRole,resourceGroup().id)
  scope: resourceGroup()
  properties: {
    principalType: 'ServicePrincipal'
    principalId: identity.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', fhirDataImporterRole)
  }
}

output identityId string = identity.id
output identityClientId string = identity.properties.clientId
output identityPrincipalId string = identity.properties.principalId
