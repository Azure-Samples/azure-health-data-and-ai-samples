@description('Resource ID of the FHIR service to grant access to.')
param fhirId string

@description('Object ID of the user, group, or service principal to grant access to.')
param principalId string

@description('Type of principal. User for human deployers, ServicePrincipal for managed identities or app registrations.')
@allowed([
  'User'
  'ServicePrincipal'
  'Group'
])
param principalType string = 'ServicePrincipal'

@description('FHIR role to assign.')
@allowed([
  'fhirContributor'
  'fhirSmart'
])
param roleType string

// FHIR Data Contributor — full data plane access (read/write FHIR resources).
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#fhir-data-contributor
resource fhirContributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  name: '5a1fc7df-4bf1-4951-a576-89034ee01acd'
}

// FHIR SMART User — access via SMART on FHIR scopes.
// https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#fhir-smart-user
resource fhirSmartRoleDefinition 'Microsoft.Authorization/roleDefinitions@2018-01-01-preview' existing = {
  scope: subscription()
  name: '4ba50f17-9666-485c-a643-ff00808643f0'
}

resource fhirContributorAccess 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (roleType == 'fhirContributor') {
  name: guid(fhirId, principalId, fhirContributorRoleDefinition.id)
  properties: {
    roleDefinitionId: fhirContributorRoleDefinition.id
    principalId: principalId
    principalType: principalType
  }
}

resource fhirSmartAccess 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if (roleType == 'fhirSmart') {
  name: guid(fhirId, principalId, fhirSmartRoleDefinition.id)
  properties: {
    roleDefinitionId: fhirSmartRoleDefinition.id
    principalId: principalId
    principalType: principalType
  }
}
