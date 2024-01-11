@description('The name of the Azure App Insights that contains DICOMcast telemetry.')
param appInsightsName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The maximum number of events per batch sent from Azure Event Grid to the DICOMcast Azure Function.')
param dicomEventMaxBatchSize int = 100

@description('The name of the Azure Event Grid subscription that routes events to the DICOMcast Azure Function.')
param dicomEventSubscriptionName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The name of the Azure Event Grid System Topic for DICOM events.')
param dicomEventTopicName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The name of the Azure DICOM Service.')
param dicomServiceName string

@description('The FHIR version.')
@allowed([
  'fhir-R4'
  'fhir-Stu3'
])
param fhirServiceKind string = 'fhir-R4'

@description('The name of the Azure FHIR Service.')
param fhirServiceName string

@description('The name of the Azure Function App that hosts DICOMcast.')
param functionAppName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The name of the Azure Function App that hosts DICOMcast.')
param functionAppZip string = 'https://github.com/Azure-Samples/azure-health-data-and-ai-samples/blob/main/samples/dicom-to-fhir/templates/dicom-cast.zip'

@description('The name of the healthcare workspace.')
param healthcareWorkspaceName string

@description('The name of user-assigned managed identity used when communicating between the functions and healthcare services.')
param identityName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The Azure region containing the resources.')
param location string = resourceGroup().location

@description('The name of the Log Analytics Workspace that contains DICOMcast telemetry.')
param logAnalyticsWorkspaceName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The pricing tier PerGB2018 or legacy tiers (Free, Standalone, PerNode, Standard or Premium) which are not available to all customers.')
@allowed([
  'CapacityReservation'
  'Free'
  'LACluster'
  'PerGB2018'
  'PerNode'
  'Premium'
  'Standalone'
  'Standard'
])
param logAnalyticsWorkplaceSku string = 'PerGB2018'

@description('The number of days to retain telemetry data.')
param logAnalyticsWorkplaceRetentionInDays int = 120

@description('The name of the Azure Storage Account that maintains the state of Azure Functions.')
param storageAccountName string = 'dicomcast${uniqueString(resourceGroup().id)}'

@description('The Azure Storage Account type.')
@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
])
param storageAccountType string = 'Standard_LRS'

var dicomDataReader = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'e89c7a3c-2f64-4fa1-a847-3e4c9ba4283a')
var fhirDataContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5a1fc7df-4bf1-4951-a576-89034ee01acd')
var storageBlobContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var storageQueueContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var storageTableContributor = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var monitoringMetricsPublisher = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  location: location
  name: identityName
}

resource healthcareWorkspace 'Microsoft.HealthcareApis/workspaces@2023-02-28' = {
  location: location
  name: healthcareWorkspaceName
}

resource dicom 'Microsoft.HealthcareApis/workspaces/dicomservices@2023-02-28' = {
  location: location
  name: dicomServiceName
  parent: healthcareWorkspace
  properties: {}
}

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2023-02-28' = {
  kind: fhirServiceKind
  location: location
  name: fhirServiceName
  parent: healthcareWorkspace
  properties: {
    authenticationConfiguration: {
      audience: 'https://${fhirServiceName}.azurehealthcareapis.com'
      authority: uri(environment().authentication.loginEndpoint, subscription().tenantId)
    }
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  kind: 'Storage'
  location: location
  name: storageAccountName
  properties: {
    defaultToOAuthAuthentication: true
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: false
      services: {
        blob: {
          enabled: true
        }
        file: {
          enabled: true
        }
        queue: {
          enabled: true
        }
        table: {
          enabled: true
        }
      }
    }
    supportsHttpsTrafficOnly: true
  }
  sku: {
    name: storageAccountType
  }
}

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  location: location
  name: logAnalyticsWorkspaceName
  properties: {
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    retentionInDays: logAnalyticsWorkplaceRetentionInDays
    sku: {
      name: logAnalyticsWorkplaceSku
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  kind: 'web'
  location: location
  name: appInsightsName
  properties: {
    Application_Type: 'web'
    DisableLocalAuth: true
    Request_Source: 'rest'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

resource dicomDataReaderRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, dicomServiceName)
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: dicomDataReader
  }
  scope: dicom
}

resource fhirDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, fhirServiceName)
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: fhirDataContributor
  }
  scope: fhir
}

resource storageBlobContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'blob')
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: storageBlobContributor
  }
  scope: storageAccount
}

resource storageQueueContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'queue')
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: storageQueueContributor
  }
  scope: storageAccount
}

resource storageTableContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, identityName, storageAccountName, 'table')
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: storageTableContributor
  }
  scope: storageAccount
}

resource monitoringMetricsPublisherRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().subscriptionId, appInsightsName)
  properties: {
    principalId: managedIdentity.properties.principalId
    roleDefinitionId: monitoringMetricsPublisher
  }
  scope: applicationInsights
}

resource hostingPlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  kind: 'linux'
  location: location
  name: functionAppName
  properties: {}
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource functionApp 'Microsoft.Web/sites@2022-09-01' = {
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
    httpsOnly: true
    serverFarmId: hostingPlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
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
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED'
          value: '1'
        }
      ]
      ftpsState: 'FtpsOnly'
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      minTlsVersion: '1.2'
      use32BitWorkerProcess: false
    }
  }
}

resource systemTopic 'Microsoft.EventGrid/systemTopics@2022-06-15' = {
  location: location
  name: dicomEventTopicName
  properties: {
    source: dicom.id
    topicType: 'Microsoft.HealthcareApis.Workspaces.Dicomservices'
  }
}

resource eventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2022-06-15' = {
  name: dicomEventSubscriptionName
  parent: systemTopic
  properties: {
    destination: {
      endpointType: 'AzureFunction'
      properties: {
        maxEventsPerBatch: dicomEventMaxBatchSize
        resourceId: functionApp.id
      }
    }
    eventDeliverySchema: 'CloudEventSchemaV1_0'
    filter: {
      includedEventTypes: [
        'Microsoft.HealthcareApis.DicomImageCreated'
        'Microsoft.HealthcareApis.DicomImageDeleted'
        'Microsoft.HealthcareApis.DicomImageUpdated'
      ]
    }
  }
}

resource functionAppZipDeploy 'Microsoft.Web/sites/extensions@2021-02-01' = {
  name:  'MSDeploy'
  parent: functionApp
  properties: {
    packageUri: functionAppZip
  }
}

