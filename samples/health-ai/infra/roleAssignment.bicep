param fhirservicename string
param dicomservicename string
param fhirContributorRoleAssignmentId string
param dicomOwnerRoleAssignmentId string
param dicomReaderRoleAssignmentId string
param principalId string
param principalType string = 'ServicePrincipal'
param principalTypeUser string='User'
param subscriptionid string = subscription().subscriptionId
param storageAccountName string
param storageBlobDataContributorRole string
param userPrincipalId string
param dataFactoryName string
param managedIdentityName string
param keyVaultName string

resource FHIR 'Microsoft.HealthcareApis/workspaces/fhirservices@2022-06-01' existing =  {
  name: fhirservicename
}

resource DICOM 'Microsoft.HealthcareApis/workspaces/dicomservices@2022-06-01' existing =  {
  name: dicomservicename
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageAccountName
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' existing={
  name:keyVaultName
}

resource fhirServiceRoleAssignment 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' =  {
   name: guid(principalId,FHIR.id,fhirContributorRoleAssignmentId)
  scope: FHIR
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${fhirContributorRoleAssignmentId}'
    principalId: principalId
    principalType: principalType
  }
}

resource dicomOwnerRoleAssignmentApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(principalType,DICOM.id,dicomOwnerRoleAssignmentId)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomOwnerRoleAssignmentId}'
    principalId: principalId
    principalType: principalType
  }
}

resource dicomOwnerRoleAssignmentUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(DICOM.id,dicomOwnerRoleAssignmentId,principalTypeUser)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomOwnerRoleAssignmentId}'
    principalId: userPrincipalId
    principalType: principalTypeUser
  }
}
resource dicomOwnerRoleAssignmentFactory 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(DICOM.id,dicomOwnerRoleAssignmentId,principalType)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomOwnerRoleAssignmentId}'
    principalId: reference('Microsoft.DataFactory/factories/${dataFactoryName}', '2018-06-01', 'Full').identity.principalId
    principalType:principalType
  }
}

resource dicomReaderRoleAssignmentApp 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(principalType,DICOM.id,dicomOwnerRoleAssignmentId,dicomservicename)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomReaderRoleAssignmentId}'
    principalId: principalId
    principalType: principalType
  }
}

resource dicomReaderRoleAssignmentUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
   name: guid(principalId,DICOM.id)
   scope: DICOM
   properties: {
     roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomReaderRoleAssignmentId}'
     principalId: userPrincipalId
     principalType: principalTypeUser
   }
 }
 resource dicomReaderRoleAssignmentFactory 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(principalId,DICOM.id,dicomReaderRoleAssignmentId,principalType)
  scope: DICOM
  properties: {
    roleDefinitionId: '/subscriptions/${subscriptionid}/providers/Microsoft.Authorization/roleDefinitions/${dicomReaderRoleAssignmentId}'
    principalId: reference('Microsoft.DataFactory/factories/${dataFactoryName}', '2018-06-01', 'Full').identity.principalId
    principalType:principalType
  }
}

resource roleAssignmentStorageUser 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccountName,principalTypeUser)
  scope:storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRole
    principalId: userPrincipalId
    principalType:principalTypeUser
  }
}
resource roleAssignmentStorageApp 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(storageAccountName,principalTypeUser,storageAccountName)
  scope:storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRole
    principalId: principalId
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

resource roleassignmentKeyVault 'Microsoft.Authorization/roleAssignments@2020-04-01-preview'={
  name:guid(keyVaultName,principalTypeUser)
  scope:keyvault
  properties: {
    roleDefinitionId: '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/00482a5a-887f-4fb3-b363-3b7fe8e74483'
    principalId: userPrincipalId
    principalType:principalTypeUser
  } 
}
