@description('The name of the Azure App Insights that contains DICOMcast telemetry.')
param appInsightsName string = 'dicomcastappinsights'

@description('The name of the Azure DICOM Service.')
param dicomServiceName string

@description('The name of the Azure FHIR Service.')
param fhirServiceName string

@description('The name of the Azure Function App that hosts DICOMcast.')
param functionAppName string

@description('The name of user-assigned managed identity used when communicating between the functions and healthcare services.')
param identityName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The Azure region containing the resources.')
param location string = resourceGroup().location

@description('The name of the Azure Storage Account that maintains the state of Azure Functions.')
param storageAccountName string = 'cast${uniqueString(resourceGroup().id)}'

@description('The Azure Storage Account type.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param storageAccountType string = 'Standard_LRS'

@description('The name of the healthcare workspace.')
param workspaceName string

var dicomDataReader = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e89c7a3c-2f64-4fa1-a847-3e4c9ba4283a')
var fhirDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5a1fc7df-4bf1-4951-a576-89034ee01acd')
var storageBlobContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var storageQueueContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var monitoringMetricsPublisher = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')

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
  name: fhirServiceName
  location: location
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountType
  }
  kind: 'Storage'
  properties: {
    supportsHttpsTrafficOnly: true
    defaultToOAuthAuthentication: true
    encryption: {
      keySource: 'Microsoft.Storage'
      services: {
        blob: {
          enabled: true
        }
        file: {
          enabled: true
        }
        table: {
          enabled: true
          keyType: 'Account'
        }
        queue: {
          enabled: true
          keyType: 'Account'
        }
      }
      requireInfrastructureEncryption: false
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
    DisableLocalAuth: false
  }
}

resource dicomDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, dicomServiceName)
  scope: dicom
  properties: {
    roleDefinitionId: dicomDataReader
    principalId: managedIdentity.properties.principalId
  }
}

resource fhirDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, fhirServiceName)
  scope: fhir
  properties: {
    roleDefinitionId: fhirDataContributor
    principalId: managedIdentity.properties.principalId
  }
}

resource storageBlobContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'blob')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobContributor
    principalId: managedIdentity.properties.principalId
  }
}

resource storageQueueContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'queue')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageQueueContributor
    principalId: managedIdentity.properties.principalId
  }
}

resource storageTableContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'table')
  scope: storageAccount
  properties: {
    roleDefinitionId: storageTableContributor
    principalId: managedIdentity.properties.principalId
  }
}

resource monitoringMetricsPublisherRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, appInsightsName)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisher
    principalId: managedIdentity.properties.principalId
  }
}

resource hostingPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: functionAppName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  dependsOn: [
    dicomDataReaderRoleAssignment
    fhirDataContributorRoleAssignment
    storageBlobContributorRoleAssignment
    storageQueueContributorRoleAssignment
    storageTableContributorRoleAssignment
    monitoringMetricsPublisherRoleAssignment
  ]
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage__AccountName'
          value: storageAccountName
        }
        {
          name: 'AzureWebJobsStorage__Credential'
          value: 'ManagedIdentity'
        }
        {
          name: 'AzureWebJobsStorage__ClientId'
          value: managedIdentity.properties.clientId
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
      ]
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
    }
    httpsOnly: true
  }
}
