param location string
param appTags object = {}
param environment string = 'dev'
param name string
@secure()
param key string
param funcAppName string
param funcURL string
param storageAccountName string
param storagekey string

resource storage 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: 'st${name}logic${environment}'
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_GRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
  }
  tags: appTags
}

resource plan 'Microsoft.Web/serverfarms@2021-02-01' = {
  name: 'plan-${name}-logic-${environment}'
  location: location
  sku: {
    tier: 'WorkflowStandard'
    name: 'WS1'
  }
  properties: {
    targetWorkerCount: 2
    maximumElasticWorkerCount: 20
    elasticScaleEnabled: true
    isSpot: false
    zoneRedundant: true
  }
}

resource logws 'Microsoft.OperationalInsights/workspaces@2021-06-01' = {
  name: 'log-${name}-${environment}'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018' // Standard
    }
  }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${name}-logic-${environment}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Flow_Type: 'Bluefield'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    Request_Source: 'rest'
    RetentionInDays: 30
    WorkspaceResourceId: logws.id
  }
}

resource site 'Microsoft.Web/sites@2021-02-01' = {
  name: 'logic-${name}-${environment}'
  location: location
  kind: 'functionapp,workflowapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    httpsOnly: true
    serverFarmId: plan.id
    clientAffinityEnabled: false
  }
  dependsOn: [
    storage
  ]
}

resource connection 'Microsoft.Web/connections@2016-06-01'= {
  name: 'BlobConnection'
  location: location
  kind: 'V2'
  properties: {
      displayName: 'BlobConnection'
      statuses: [
          {
              status: 'Connected'
          }
      ]
      api: {
          name: 'azureblob'
          displayName: 'Azure Blob Storage'
          description: 'Microsoft Azure Storage provides a massively scalable, durable, and highly available storage for data on the cloud, and serves as the data storage solution for modern applications. Connect to Blob Storage to perform various operations such as create, update, get and delete on blobs in your Azure Storage account.'
          iconUri: 'https://connectoricons-prod.azureedge.net/releases/v1.0.1608/1.0.1608.3079/azureblob/icon.png'   
          brandColor: '#804998'
          id: subscriptionResourceId('Microsoft.Web/locations/managedApis', location, 'azureblob')
          type: 'Microsoft.Web/locations/managedApis'
      }
      parameterValues: {
        accountName: storageAccountName
        accessKey: storagekey
      }
  }
  dependsOn: [
    site
  ]
}

resource policy 'Microsoft.Web/connections/accessPolicies@2016-06-01' = {
  name: 'BlobConnection/Access_policy'
  location: location
  properties: {
    principal: {
      type: 'ActiveDirectory'
      identity: {
        tenantId: subscription().tenantId
        objectId: site.identity.principalId
      }
    }
  }
  dependsOn: [
    connection
  ]
}


resource logicAppSetting 'Microsoft.Web/sites/config@2020-12-01' = {
  name: 'appsettings'
  parent: site
  properties: {
    FUNCTIONS_EXTENSION_VERSION: '~3'
    FUNCTIONS_WORKER_RUNTIME: 'node'
    WEBSITE_NODE_DEFAULT_VERSION: '~12'
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING : 'DefaultEndpointsProtocol=https;AccountName=${storage.name};AccountKey=${listKeys(storage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
    WEBSITE_CONTENTSHARE : 'app-${toLower(name)}-logicservice-${toLower(environment)}a6e9'
    AzureFunctionsJobHost__extensionBundle__id : 'Microsoft.Azure.Functions.ExtensionBundle.Workflows'
    AzureFunctionsJobHost__extensionBundle__version: '[1.*, 2.0.0)'
    APP_KIND : 'workflowApp'
    APPINSIGHTS_INSTRUMENTATIONKEY: appi.properties.InstrumentationKey
    ApplicationInsightsAgent_EXTENSION_VERSION:'~2'
    APPLICATIONINSIGHTS_CONNECTION_STRING : appi.properties.ConnectionString
    subscription_Id : subscription().id
    resource_group : resourceGroup().name
    azureFunctionOperation_functionAppKey : key
    site_name : funcAppName
    Location : location
    HL7Validate_URL : '${funcURL}/validatehl7'
    HL7Sequencing_URL : '${funcURL}/hl7sequencing'
    HL7Converter_URL : '${funcURL}/hl7converter'
    FHIRPostProcessing_URL : '${funcURL}/fhirpostprocessfunction'
    FHIRUpload_URL : '${funcURL}/uploadfhirjson'
    connectionRuntimeUrl : reference(connection.id, connection.apiVersion, 'full').properties.connectionRuntimeUrl
  }
  dependsOn: [
    site
    connection
  ]
}
