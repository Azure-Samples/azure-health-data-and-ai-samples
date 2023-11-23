param dicomservicename string
param principalType string = 'ServicePrincipal'
param subscriptionid string = subscription().subscriptionId
param storageAccountName string
param storageBlobDataContributorRole string
param dataFactoryName string
param managedIdentityName string

resource DICOM 'Microsoft.HealthcareApis/workspaces/dicomservices@2022-06-01' existing =  {
  name: dicomservicename
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageAccountName
}

resource dicomOwnerRoleAssignmentFactory 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(DICOM.id,principalType)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/58a3b984-7adf-4c20-983a-32417c86fbc8'
    principalId: reference('Microsoft.DataFactory/factories/${dataFactoryName}', '2018-06-01', 'Full').identity.principalId
    principalType:principalType
  }
}

 resource dicomReaderRoleAssignmentFactory 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(DICOM.id,principalType,'reader')
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/e89c7a3c-2f64-4fa1-a847-3e4c9ba4283a'
    principalId: reference('Microsoft.DataFactory/factories/${dataFactoryName}', '2018-06-01', 'Full').identity.principalId
    principalType:principalType
  }
}

resource roleassignmentfactory 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name:guid(storageAccountName,dataFactoryName,principalType)
  scope:storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRole
    principalId: reference('Microsoft.DataFactory/factories/${dataFactoryName}', '2018-06-01', 'Full').identity.principalId
    principalType:principalType
  }
}

resource roleassignmentManagedIdentity 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name:guid(storageAccountName,managedIdentityName)
  scope:storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRole
    principalId: reference('Microsoft.ManagedIdentity/userAssignedIdentities/${managedIdentityName}', '2023-01-31').principalId
    principalType:principalType
  }
}
