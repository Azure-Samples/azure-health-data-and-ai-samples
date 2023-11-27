param dicomServiceName string
param identityName string = 'cast${uniqueString(resourceGroup().id)}'
param location string = resourceGroup().location
param workspaceName string

var dicomDataReader = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e89c7a3c-2f64-4fa1-a847-3e4c9ba4283a')
var fhirDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5a1fc7df-4bf1-4951-a576-89034ee01acd')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource workspace 'Microsoft.HealthcareApis/workspaces@2023-02-28' = {
  name: workspaceName
  location: location
}

resource dicom 'Microsoft.HealthcareApis/workspaces/dicomservices@2023-02-28' = {
  parent: workspace
  name: dicomServiceName
  location: location
}

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-02-28' = {
  parent: workspace
  name: 'fhir'
  location: location
}
