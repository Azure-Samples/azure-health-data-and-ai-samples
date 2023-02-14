@description('Name prefix for all resources.')
@minLength(3)
@maxLength(24)
param name string
@description('Location to deploy resources.')
param location string
@description('The FHIR Service endpoint to export data from.')
param fhirServiceUrl string
@description('Name of the Azure Datalake Storage Gen 2 Storage Account')
param storageAccountName string
@description('Tags for resources')
param tags object = {}
@allowed([
  'R4'
])
param fhirVersion string = 'R4'
@description('Start timestamp of the data range you want to export.')
param dataStart string = '1970-01-01 00:00:00 +00:00'
@description('End timestamp of the data range you want to export. Will continuous export all data if not specified.')
param dataEnd string = ''
@description('The name of the container to store job and data.')
param containerName string = 'fhir'
@description('The fhir-to-synapse pipeline package url.')
#disable-next-line no-hardcoded-env-urls
param packageUrl string = 'https://github.com/microsoft/FHIR-Analytics-Pipelines/releases/download/v0.6.0/Microsoft.Health.Fhir.Synapse.FunctionApp.zip'
@description('Log Analytics workspace resource id for linking to Application Insights.')
param logAnalyticsWorkspaceId string

@description('Used to pull keys from existing function storage account')
resource functionStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageAccountName
}

var hostingPlanName = '${name}-host'
@description('Hosting plan for the FHIR to Analytics pipeline')
resource functionHostingPlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: hostingPlanName
  location: location
  tags: tags
  sku: {
    tier: 'ElasticPremium'
    name: 'EP3'
  }
  properties: {
    reserved: true
    elasticScaleEnabled: true
  }
}

var functionAppName = '${name}-fa'
@description('Function app for FHIR to Analytics pipeline')
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  kind: 'functionapp,linux'
  properties: {
    serverFarmId: functionHostingPlan.id
    reserved: true
    siteConfig: {
      linuxFxVersion: 'dotnet-isolated|6.0'
      use32BitWorkerProcess: false
    }
  }

  resource functionAppSettings 'config@2020-12-01' = {
    name: 'appsettings'
    properties: {
      AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${functionStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(functionStorageAccount.id, '2019-06-01').keys[0].value}'
      WEBSITE_CONTENTAZUREFILECONNECTIONSTRING: 'DefaultEndpointsProtocol=https;AccountName=${functionStorageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${listKeys(functionStorageAccount.id, '2019-06-01').keys[0].value}'
      WEBSITE_CONTENTSHARE: toLower(functionAppName)
      FUNCTIONS_EXTENSION_VERSION: '~4'
      FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
      LD_LIBRARY_PATH: '/home/site/wwwroot'
      WEBSITE_RUN_FROM_PACKAGE: packageUrl
      APPINSIGHTS_INSTRUMENTATIONKEY: appInsights.properties.InstrumentationKey
      APPLICATIONINSIGHTS_CONNECTION_STRING: 'InstrumentationKey=${appInsights.properties.InstrumentationKey}'
      job__jobInfoQueueName: '${name}jobinfoqueue'
      job__jobInfoTableName: '${name}jobinfotable'
      job__metadataTableName: '${name}metadatatable'
      job__maxRunningJobCount: '3'
      job__containerName: containerName
      job__startTime: dataStart
      job__endTime: dataEnd
      job__tableUrl: 'https://${functionStorageAccount.name}.table.${environment().suffixes.storage}'
      job__queueUrl: 'https://${functionStorageAccount.name}.queue.${environment().suffixes.storage}'
      dataLakeStore__storageUrl: 'https://${functionStorageAccount.name}.blob.${environment().suffixes.storage}'
      filter__filterScope: 'System'
      filter__enableExternalFilter: 'false'
      filter__filterImageReference: ''
      filter__groupId: ''
      filter__requiredTypes: ''
      filter__typeFilters: ''
      schema__enableCustomizedSchema: 'false'
      fhirServer__serverUrl: fhirServiceUrl
      fhirServer__version: fhirVersion
      fhirServer__authentication: 'ManagedIdentity'
    }
  }
}

var appInsightsName = '${name}-ai'

@description('Monitoring for Function App')
resource appInsights 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    IngestionMode: 'LogAnalytics'
    WorkspaceResourceId: logAnalyticsWorkspaceId
  }
  tags: tags
}

output functionAppName string = functionAppName
output functionAppPrincipalId string = functionApp.identity.principalId
output hostName string = functionApp.properties.defaultHostName
