@description('Name of your Storage Account.')
param storageAccountName string = ''

@description('The Azure region into which the resources should be deployed.')
param location string = resourceGroup().location

@description('Name of the queue associated with the storage account.')
param queueName string = ''

@description('Name of the Ingest Container Name.')
param ingestContainerName string = ''

@description('Name of the Processed Container Name')
param processedContainerName string=''

@description('Name of the IdpDicom Container Name')
param idpDicomContainerName string =''

@description('Name of FHIR Data Container ')
param fhirDataContainerName string

@description('Name of the Event Grid subscription.')
param eventGridSubscriptionName string = ''

@description('Name of your Azure Health Data Services workspace.')
param workspaceName string = ''

@minLength(3)
@description('Name of your Fhir Service.')
param fhirName string 

@minLength(3)
@description('Name of your Dicom Service.')
param dicomName string

@description('Automatically create a role assignment for the function app to access the FHIR service.')
param createRoleAssignment bool = true

@description('Unique identifier for a principal')
param principalId string

@description('Unique identifier for a user principal')
param userPrincipalId string

@description('Unique identifier for a FHIR contributor role assignment')
param fhirContributorRoleAssignmentId string = '5a1fc7df-4bf1-4951-a576-89034ee01acd'

@description('Unique identifier for a DICOM owner role assignment')
param dicomOwnerRoleAssignmentId string = '58a3b984-7adf-4c20-983a-32417c86fbc8'

@description('Unique identifier for a DICOM reader role assignment')
param dicomReaderRoleAssignmentId string = 'e89c7a3c-2f64-4fa1-a847-3e4c9ba4283a'

@description('Name of the storageQueue Processing Functions App.')
param storageQueueProcessingAppName string

@description('Name of Storage Account for Storage Queue Processing')
param storageQProcessingStorageName string  

@description('Specifies if the Azure Functions App should always be on.')
param alwaysOn bool=false

@description('Name of the Storage Queue Processing App Service Plan.')
param storageQProcessingPlanName string

@description('Data Factory Name')
param dataFactoryName string 

@description('linked service for the DICOM service')
param RestServicename string='RestService1'

@description('linked service for Azure Data Lake Storage Gen2')
param AzureDataLakeStoragename string='AzureDataLakeStorage1'

@description('Name of Key Vault')
param keyVaultName string

@description('URL of the GitHub repository.')
param repoUrl string= 'https://github.com/Azure-Samples/azure-health-data-and-ai-samples/'

var tenantId= subscription().tenantId
var fhirservicename = '${workspaceName}/${fhirName}'
var dicomservicename = '${workspaceName}/${dicomName}'
var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var audience = 'https://${workspaceName}-${fhirName}.fhir.azurehealthcareapis.com'
var storageBlobDataContributorRole = '/subscriptions/${subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var subscriptionid=subscription().subscriptionId
var resourcegroupname=resourceGroup().name
var managedIdentityName='${workspaceName}-${dicomName}'
var pipelineName = 'Copy IDP DICOM Metadata Changes to ADLS Gen2 in Delta Formatt'

resource storageAccount 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    isHnsEnabled: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    encryption: {
      keySource: 'Microsoft.Storage'
      requireInfrastructureEncryption: true
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
      }
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: false
      days: 7
    }
    containerDeleteRetentionPolicy: {
      enabled: false
      days: 7
    }
  }
}

resource ingestContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2021-08-01' = {
  parent: blobService
  name: ingestContainerName 
}

resource processedContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: processedContainerName
}

resource idpDicomContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: idpDicomContainerName
}

resource fhirDataContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: fhirDataContainerName
}

resource storageQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2021-08-01' = {
  name: '${storageAccountName}/default/${queueName}'
  dependsOn: [ storageAccount ]
}

resource eventGridSubscription 'Microsoft.EventGrid/eventSubscriptions@2023-06-01-preview' = {
  name: eventGridSubscriptionName
  scope: storageAccount
  properties: {
    destination: {
      endpointType: 'StorageQueue'
      properties: {
        resourceId:storageAccount.id
        queueName: queueName
        queueMessageTimeToLiveInSeconds: -1
      }
    }
    filter: {
      includedEventTypes: [
        'Microsoft.Storage.BlobCreated'
        'Microsoft.Storage.BlobDeleted'
      ]
      subjectBeginsWith: '/blobServices/default/containers/${ingestContainerName}'              
      enableAdvancedFilteringOnArrays: true
    }
    labels: []
    eventDeliverySchema: 'EventGridSchema'
  }
}

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

resource Workspace 'Microsoft.HealthcareApis/workspaces@2022-06-01' = {
  name: workspaceName
  location: location
}

resource FHIR 'Microsoft.HealthcareApis/workspaces/fhirservices@2022-06-01' = {
  name: fhirservicename
  location: location
  kind: 'fhir-R4'
  identity: {
    type: 'SystemAssigned'
  }
  dependsOn: [
    Workspace 
  ]
  properties: {
    authenticationConfiguration: {
      authority: authority
      audience: audience
      smartProxyEnabled: false
    }
  }
}

resource DICOM 'Microsoft.HealthcareApis/workspaces/dicomservices@2022-06-01' =  {
  name: dicomservicename
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {
      }
    }
  }
  dependsOn: [
    Workspace

  ]
  properties:{
    storageConfiguration:{
      accountName:storageAccountName
      containerName:ingestContainerName
    }
  }
}

resource keyvault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enableRbacAuthorization: true
    provisioningState: 'Succeeded'
    publicNetworkAccess: 'Enabled'
  }
}
resource keySecretsFhirAuthUrl 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'fhirAuthurl'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsFhirClientId 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'fhirClientId'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsFhirClientSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'fhirClientSecret'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsFhirUrl 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'fhirUrl'
  properties: {
    contentType:'text/plain'
    value:'https://${workspaceName}-${fhirName}.fhir.azurehealthcareapis.com'
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsAiApiKey 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'aiApiKey'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}

resource keySecretsAiResourceName 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'aiResourceName'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsAiDeployment 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'aiDeploymentName'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}
resource keySecretsAiApiVersion 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyvault
  name: 'aiApiVersion'
  properties: {
    contentType:'text/plain'
    value:' '
    attributes: {
      enabled: true  
    }
  }
}

resource storageQueueProcessingApp 'Microsoft.Web/sites@2021-03-01' = {
  name: storageQueueProcessingAppName
  kind: 'functionapp'
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    enabled: true
    clientAffinityEnabled: false
    httpsOnly: true
    serverFarmId:storageQProcessinghostingPlan.id

    siteConfig: {
      alwaysOn:alwaysOn
      appSettings: [
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: reference('microsoft.insights/components/${storageQueueProcessingAppName}', '2015-05-01').ConnectionString
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageQProcessingStorageName};AccountKey=${listKeys(storageQProcessingStorage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageQProcessingStorageName};AccountKey=${listKeys(storageQProcessingStorage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
        }
      ] 
    }  
  }
  dependsOn: [
    storageQueueProcessingAppInsight
    storageQProcessingStorage
  ]
  resource config 'config' = {
    name: 'web'
    properties: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
  }
  resource ftpPublishingPolicy 'basicPublishingCredentialsPolicies' = {
    name: 'ftp'
    location: location
    properties: {
      allow: false
    }
  }

  resource scmPublishingPolicy 'basicPublishingCredentialsPolicies' = {
    name: 'scm'
    location: location
    properties: {
      allow: false
    }
  }
}

resource storageQueueProcessingConfig 'Microsoft.Web/sites/config@2021-03-01' = {
  name: 'appsettings'
  parent: storageQueueProcessingApp
  properties: {
    FUNCTIONS_EXTENSION_VERSION: '~4'
    FUNCTIONS_WORKER_RUNTIME: 'dotnet-isolated'
    AzureWebJobsStorage: 'DefaultEndpointsProtocol=https;AccountName=${storageQProcessingStorageName};AccountKey=${listKeys(storageQProcessingStorage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
    fhirUri: format('https://{0}.fhir.azurehealthcareapis.com', replace(fhirservicename, '/', '-'))  
    DicomUri: format('https://{0}.dicom.azurehealthcareapis.com', replace(dicomservicename, '/', '-'))
    fhirHttpClient: format('https://{0}.fhir.azurehealthcareapis.com', replace(fhirservicename, '/', '-'))
    dicomHttpClient: format('https://{0}.dicom.azurehealthcareapis.com/v1', replace(dicomservicename, '/', '-'))
    dicomResourceUri: 'https://dicom.healthcareapis.azure.com'
    storageAccountName:storageQProcessingStorageName
    sourceContainerName: ingestContainerName
    processedContainerName:processedContainerName
    storageConnection: 'DefaultEndpointsProtocol=https;AccountName=${storageQProcessingStorageName};AccountKey=${listKeys(storageQProcessingStorage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
    AppInsightConnectionString: reference('microsoft.insights/components/${storageQueueProcessingAppName}', '2015-05-01').ConnectionString
    APPINSIGHTS_INSTRUMENTATIONKEY:reference('microsoft.insights/components/${storageQueueProcessingAppName}', '2015-05-01').InstrumentationKey
    WEBSITE_CONTENTSHARE:storageQueueProcessingAppName
    WEBSITE_CONTENTAZUREFILECONNECTIONSTRING:'DefaultEndpointsProtocol=https;AccountName=${storageQProcessingStorageName};AccountKey=${listKeys(storageQProcessingStorage.id, '2019-06-01').keys[0].value};EndpointSuffix=core.windows.net'
  }
}

@description('App Service used to run Azure Function')
resource storageQProcessinghostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: storageQProcessingPlanName
  location: location
  kind: 'functionapp'
  properties: {
    targetWorkerCount: 2
    reserved: true
  }
  sku: {
    tier: 'Dynamic'
    name: 'Y1'
  }
}

@description('Monitoring for Function App')
resource storageQueueProcessingAppInsight 'microsoft.insights/components@2020-02-02-preview' = {
  name: storageQueueProcessingAppName
  location:location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

@description('Azure Function required linked storage account')
resource storageQProcessingStorage 'Microsoft.Storage/storageAccounts@2022-05-01' = {
  name: storageQProcessingStorageName
  location: location
  kind:'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    defaultToOAuthAuthentication: true
  }  
}


resource dataFactory 'Microsoft.DataFactory/factories@2018-06-01' = {
  name: dataFactoryName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
}

resource RestService 'Microsoft.DataFactory/factories/linkedServices@2018-06-01' = {
  parent: dataFactory
  name: RestServicename
  properties: {
    annotations: []
    type: 'RestService'
    typeProperties: {
      url:'https://${managedIdentityName}.dicom.azurehealthcareapis.com'
      enableServerCertificateValidation: true
      authenticationType: 'ManagedServiceIdentity'
      aadResourceId: 'https://dicom.healthcareapis.azure.com'
    }
  }
  dependsOn: []
}

resource AzureDataLakeStorage 'Microsoft.DataFactory/factories/linkedServices@2018-06-01' = {
  parent: dataFactory
  name: AzureDataLakeStoragename
  properties: {
    annotations: []
    type: 'AzureBlobFS'
    typeProperties: {
      url: 'https://${storageAccountName}.dfs.core.windows.net/'
    }
  }
  dependsOn: []
}

resource pipeline 'Microsoft.DataFactory/factories/pipelines@2018-06-01' = {
  parent: dataFactory
  name: pipelineName
  properties: {
    activities: [
      {
        name: 'Update Delta Tables'
        description: 'Read the change feed in batches and synchronize any changes with Delta Tables.'
        type: 'Until'
        dependsOn: []
        userProperties: []
        typeProperties: {
          expression: {
            value: '@not(variables(\'Continue\'))'
            type: 'Expression'
          }
          activities: [
            {
              name: 'Process Batch'
              description: 'Update the Delta Tables using a page of data from the Change Feed.'
              type: 'ExecuteDataFlow'
              dependsOn: []
              policy: {
                timeout: '0.12:00:00'
                retry: 0
                retryIntervalInSeconds: 30
                secureOutput: true
                secureInput: true
              }
              userProperties: []
              typeProperties: {
                dataFlow: {
                  referenceName: 'Update IDP DICOM Delta Tables'
                  type: 'DataFlowReference'
                  parameters: {
                    Offset: {
                      value: '@int(variables(\'CurrentOffset\'))'
                      type: 'Expression'
                    }
                    Limit: {
                      value: '@pipeline().parameters.BatchSize'
                      type: 'Expression'
                    }
                    ApiVersion: {
                      value: '@pipeline().parameters.ApiVersion'
                      type: 'Expression'
                    }
                    StartTime: {
                      value: '\'@{pipeline().parameters.StartTime}\''
                      type: 'Expression'
                    }
                    EndTime: {
                      value: '\'@{pipeline().parameters.EndTime}\''
                      type: 'Expression'
                    }
                    ContainerName: {
                      value: '\'@{pipeline().parameters.ContainerName}\''
                      type: 'Expression'
                    }
                    InstanceTablePath: {
                      value: '\'@{pipeline().parameters.InstanceTablePath}\''
                      type: 'Expression'
                    }
                    SeriesTablePath: {
                      value: '\'@{pipeline().parameters.SeriesTablePath}\''
                      type: 'Expression'
                    }
                    StudyTablePath: {
                      value: '\'@{pipeline().parameters.StudyTablePath}\''
                      type: 'Expression'
                    }
                    RetentionHours: {
                      value: '@pipeline().parameters.RetentionHours'
                      type: 'Expression'
                    }
                  }
                  datasetParameters: {
                    changeFeed: {
                    }
                    existingInstances: {
                    }
                    existingSeries: {
                    }
                    instanceTable: {
                    }
                    seriesTable: {
                    }
                    studyTable: {
                    }
                    seriesCache: {
                    }
                    studyCache: {
                    }
                  }
                }
                staging: {
                }
                compute: {
                  coreCount: 8
                  computeType: 'General'
                }
                traceLevel: 'Fine'
              }
            }
            {
              name: 'Determine Continuation'
              description: 'Check the previous activity for metrics related to the instance table sink to determine whether processing should continue.'
              type: 'IfCondition'
              dependsOn: [
                {
                  activity: 'Process Batch'
                  dependencyConditions: [
                    'Succeeded'
                  ]
                }
              ]
              userProperties: []
              typeProperties: {
                expression: {
                  value: '@contains(activity(\'Process Batch\').output.runStatus.metrics, \'instanceTable\')'
                  type: 'Expression'
                }
                ifFalseActivities: [
                  {
                    name: 'Complete Processing'
                    description: 'Signal that processing should stop because there are no more changes.'
                    type: 'SetVariable'
                    dependsOn: []
                    policy: {
                      // timeout: '0.12:00:00'
                      // retry: 0
                      // retryIntervalInSeconds: 30
                      secureOutput: false
                      secureInput: false
                    }
                    userProperties: []
                    typeProperties: {
                      variableName: 'Continue'
                      value: {
                        value: '@bool(\'false\')'
                        type: 'Expression'
                      }
                    }
                  }
                ]
                ifTrueActivities: [
                  {
                    name: 'Check Rows'
                    description: 'Update the continuation variable based on the number of rows processed in the last batch.'
                    type: 'SetVariable'
                    dependsOn: []
                    policy: {
                      // timeout: '0.12:00:00'
                      // retry: 0
                      // retryIntervalInSeconds: 30
                      secureOutput: false
                      secureInput: false
                    }
                    userProperties: []
                    typeProperties: {
                      variableName: 'Continue'
                      value: {
                        value: '@greater(activity(\'Process Batch\').output.runStatus.metrics.instanceTable.rowsWritten, 0)'
                        type: 'Expression'
                      }
                    }
                  }
                  {
                    name: 'Add Limit to Offset'
                    description: 'Add the limit to the current offset.'
                    type: 'SetVariable'
                    dependsOn: [
                      {
                        activity: 'Check Rows'
                        dependencyConditions: [
                          'Succeeded'
                        ]
                      }
                    ]
                    policy: {
                      // timeout: '0.12:00:00'
                      // retry: 0
                      // retryIntervalInSeconds: 30
                      secureOutput: false
                      secureInput: false
                    }
                    userProperties: []
                    typeProperties: {
                      variableName: 'Temp'
                      value: {
                        value: '@string(add(int(variables(\'CurrentOffset\')), pipeline().parameters.BatchSize))'
                        type: 'Expression'
                      }
                    }
                  }
                  {
                    name: 'Update Offset'
                    description: 'Update the current offset based on the newly computed value.'
                    type: 'SetVariable'
                    dependsOn: [
                      {
                        activity: 'Add Limit to Offset'
                        dependencyConditions: [
                          'Succeeded'
                        ]
                      }
                    ]
                    policy: {
                      // timeout: '0.12:00:00'
                      // retry: 0
                      // retryIntervalInSeconds: 30
                      secureOutput: false
                      secureInput: false
                    }
                    userProperties: []
                    typeProperties: {
                      variableName: 'CurrentOffset'
                      value: {
                        value: '@variables(\'Temp\')'
                        type: 'Expression'
                      }
                    }
                  }
                ]
              }
            }
            {
              name: 'Cancel Processing'
              description: 'Signal that processing should stop because there was a problem processing a batch.'
              type: 'SetVariable'
              dependsOn: [
                {
                  activity: 'Process Batch'
                  dependencyConditions: [
                    'Failed'
                  ]
                }
              ]
              policy: {
                // timeout: '0.12:00:00'
                // retry: 0
                // retryIntervalInSeconds: 30
                secureOutput: false
                secureInput: false
              }
              userProperties: []
              typeProperties: {
                variableName: 'Continue'
                value: {
                  value: '@bool(\'false\')'
                  type: 'Expression'
                }
              }
            }
          ]
          timeout: '0.12:00:00'
        }
      }
    ]
    policy: {
      elapsedTimeMetric: {
      }
      // cancelAfter: {
      // }
    }
    parameters: {
      BatchSize: {
        type: 'int'
        defaultValue: 200
      }
      ApiVersion: {
        type: 'int'
        defaultValue: 2
      }
      StartTime: {
        type: 'string'
        defaultValue: '0001-01-01T00:00:00Z'
      }
      EndTime: {
        type: 'string'
        defaultValue: '9999-12-31T23:59:59Z'
      }
      ContainerName: {
        type: 'string'
        defaultValue: idpDicomContainerName
      }
      InstanceTablePath: {
        type: 'string'
        defaultValue: 'instance'
      }
      SeriesTablePath: {
        type: 'string'
        defaultValue: 'series'
      }
      StudyTablePath: {
        type: 'string'
        defaultValue: 'study'
      }
      RetentionHours: {
        type: 'int'
        defaultValue: 720
      }
    }
    variables: {
      CurrentOffset: {
        type: 'String'
        defaultValue: '0'
      }
      Temp: {
        type: 'String'
        defaultValue: '0'
      }
      Continue: {
        type: 'Boolean'
        defaultValue: false
      }
    }
    annotations: []
    //lastPublishTime: '2023-06-23T19:54:06Z'
  }
  dependsOn: [
    dataflows
  ]
}

resource hourlyTrigger 'Microsoft.DataFactory/factories/triggers@2018-06-01' = {
  parent: dataFactory
  name: 'hourlytrigger7'
  properties: {
    description: 'Trigger that runs hourly'
    annotations: []
    runtimeState: 'Started'
    pipeline: {
      pipelineReference: {
        referenceName: pipelineName
        type: 'PipelineReference'
      }
      parameters: {
        StartTime:  '@trigger().outputs.windowStartTime'
        EndTime: '@trigger().outputs.windowEndTime'

      }
    }
    type: 'TumblingWindowTrigger'
    typeProperties: {
      frequency: 'Minute'
      interval: 60
      startTime: '2023-11-21T13:56:00Z'
      delay: '00:15:00'
      maxConcurrency: 1
      retryPolicy: {
        intervalInSeconds: 30
      }
    }
  }
  dependsOn: [
    pipeline
  ]
}


resource dataflows 'Microsoft.DataFactory/factories/dataflows@2018-06-01' = {
  parent: dataFactory
  name: 'Update IDP DICOM Delta Tables'
  properties: {
    type: 'MappingDataFlow'
    typeProperties: {
      sources: [
        {
          linkedService: {
            referenceName: RestServicename
            type: 'LinkedServiceReference'
          }
          name: 'changeFeed'
          description: 'Read changes from the DICOMweb server.'
        }
        {
          linkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
          name: 'existingInstances'
          description: 'Read the newly updated SOP Instance Delta Table.'
        }
        {
          linkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
          name: 'existingSeries'
          description: 'Read the newly updated Series Delta Table.'
        }
      ]
      sinks: [
        {
          linkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
          name: 'instanceTable'
          description: 'Write the changes to the SOP Instance Delta Table.'
          rejectedDataLinkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
        }
        {
          linkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
          name: 'seriesTable'
          description: 'Write the changes to the Series Delta Table.'
          rejectedDataLinkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
        }
        {
          linkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
          name: 'studyTable'
          description: 'Write the changes to the Study Delta Table.'
          rejectedDataLinkedService: {
            referenceName: AzureDataLakeStoragename
            type: 'LinkedServiceReference'
          }
        }
        {
          name: 'seriesCache'
          description: 'Write modified series identifiers to cache.'
        }
        {
          name: 'studyCache'
          description: 'Write modified study identifiers to cache.'
        }
      ]
      transformations: [
        {
          name: 'extracted'
          description: 'Extract DICOM attributes.'
        }
        {
          name: 'flattened'
          description: 'Flatten the complex object.'
        }
        {
          name: 'instanceSinkUpdates'
          description: 'Update the sink based on the action.'
        }
        {
          name: 'aggregatedChanges'
          description: 'Aggregate changes for the same SOP instance within the window.'
        }
        {
          name: 'allSeries'
          description: 'Aggregate SOP instance within the same series.'
        }
        {
          name: 'allStudies'
          description: 'Aggregate series within the same study.'
        }
        {
          name: 'seriesSinkUpdates'
          description: 'Update the sink based on the instance count.'
        }
        {
          name: 'studySinkUpdate'
          description: 'Update the sink based on the instance count.'
        }
        {
          name: 'seriesChanges'
          description: 'Determine the identifiers for modified series.'
        }
        {
          name: 'studyChanges'
          description: 'Determine the identifiers for modified studies.'
        }
        {
          name: 'modifiedSeries'
          description: 'Filter out the series which have not been updated.'
        }
        {
          name: 'annotatedSeries'
          description: 'Determines whether the series has been modified.'
        }
        {
          name: 'annotatedStudies'
          description: 'Determines whether the series has been modified.'
        }
        {
          name: 'modifiedStudies'
          description: 'Filter out the studies which have not been updated.'
        }
        {
          name: 'upToDate'
          description: 'Filter out the instances that have since been deleted or updated outside of the window.'
        }
      ]
      scriptLines: [
        'parameters{'
        '     Offset as integer (0),'
        '     Limit as integer (200),'
        '     ApiVersion as integer (2),'
        '     StartTime as string (\'0001-01-01T00:00:00Z\'),'
        '     EndTime as string (\'9999-12-31T23:59:59Z\'),'
        '     ContainerName as string (\'${idpDicomContainerName}\'),'
        '     InstanceTablePath as string (\'instance\'),'
        '     SeriesTablePath as string (\'series\'),'
        '     StudyTablePath as string (\'study\'),'
        '     RetentionHours as integer (720)'
        '}'
        'source(output('
        '          body as (action as string, metadata as (undefined as string), partitionName as string, sequence as short, seriesInstanceUid as string, sopInstanceUid as string, state as string, studyInstanceUid as string, timestamp as string, filePath as string),'
        '          headers as [string,string]'
        '     ),'
        '     allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     format: \'rest\','
        '     timeout: 30,'
        '     requestInterval: 0,'
        '     entity: (concat(\'/v\', toString($ApiVersion), \'/changefeed\')),'
        '     queryParameters: [\'includeMetadata\' -> \'true\', \'offset\' -> ($Offset), \'limit\' -> ($Limit), \'startTime\' -> ($StartTime), \'endTime\' -> ($EndTime)],'
        '     httpMethod: \'GET\','
        '     responseFormat: [\'type\' -> \'json\', \'documentForm\' -> \'arrayOfDocuments\']) ~> changeFeed'
        'source(output('
        '          partitionName as string,'
        '          studyInstanceUid as string,'
        '          seriesInstanceUid as string,'
        '          sopInstanceUid as string,'
        '          lastModifiedTimestamp as timestamp,'
        '          studyDate as date,'
        '          studyDescription as string,'
        '          issuerOfPatientId as string,'
        '          patientId as string,'
        '          patientName as string,'
        '          modality as string,'
        '          sopClassUid as string,'
        '          filePath as string'
        '     ),'
        '     allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     ignoreNoFilesFound: true,'
        '     format: \'delta\','
        '     fileSystem: ($ContainerName),'
        '     folderPath: ($InstanceTablePath)) ~> existingInstances'
        'source(output('
        '          partitionName as string,'
        '          studyInstanceUid as string,'
        '          seriesInstanceUid as string,'
        '          lastModifiedTimestamp as timestamp,'
        '          studyDate as date,'
        '          studyDescription as string,'
        '          issuerOfPatientId as string,'
        '          patientId as string,'
        '          patientName as string,'
        '          modality as string,'
        '          instanceCount as long'
        '     ),'
        '     allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     ignoreNoFilesFound: true,'
        '     format: \'delta\','
        '     fileSystem: ($ContainerName),'
        '     folderPath: ($SeriesTablePath)) ~> existingSeries'
        'flattened derive(timestamp = toTimestamp(substring(timestamp, 1, 23), \'yyyy-MM-dd\\\'T\\\'HH:mm:ss.SSS\', \'UTC\'),'
        '          studyDate = toDate(byPath(\'metadata.{00080020}.Value[1]\'), \'yyyyMMdd\', \'UTC\'),'
        '          studyDescription = toString(byPath(\'metadata.{00081030}.Value[1]\')),'
        '          issuerOfPatientId = toString(byPath(\'metadata.{00100021}.Value[1]\')),'
        '          patientId = toString(byPath(\'metadata.{00100020}.Value[1]\')),'
        '          patientName = toString(byPath(\'metadata.{00100010}.Value[1].Alphabetic\')),'
        '          modality = toString(byPath(\'metadata.{00080060}.Value[1]\')),'
        '          sopClassUid = toString(byPath(\'metadata.{00080016}.Value[1]\')),'
        '          filePath = toString(filePath)) ~> extracted'
        'changeFeed select(mapColumn('
        '          action = body.action,'
        '          timestamp = body.timestamp,'
        '          partitionName = body.partitionName,'
        '          studyInstanceUid = body.studyInstanceUid,'
        '          seriesInstanceUid = body.seriesInstanceUid,'
        '          sopInstanceUid = body.sopInstanceUid,'
        '          metadata = body.metadata,'
        '          filePath = body.filePath'
        '     ),'
        '     skipDuplicateMapInputs: false,'
        '     skipDuplicateMapOutputs: false) ~> flattened'
        'upToDate alterRow(upsertIf(or(equals(action,\'Create\'),equals(action,\'Update\'))),'
        '     deleteIf(equals(action,\'Delete\'))) ~> instanceSinkUpdates'
        'extracted aggregate(groupBy(partitionName,'
        '          studyInstanceUid,'
        '          seriesInstanceUid,'
        '          sopInstanceUid),'
        '     action = last(action),'
        '          lastModifiedTimestamp = last(timestamp),'
        '          studyDate = last(studyDate),'
        '          studyDescription = last(studyDescription),'
        '          issuerOfPatientId = last(issuerOfPatientId),'
        '          patientId = last(patientId),'
        '          patientName = last(patientName),'
        '          modality = last(modality),'
        '          sopClassUid = last(sopClassUid),'
        '          filePath = last(filePath)) ~> aggregatedChanges'
        'existingInstances aggregate(groupBy(partitionName,'
        '          studyInstanceUid,'
        '          seriesInstanceUid),'
        '     lastModifiedTimestamp = last(lastModifiedTimestamp),'
        '          studyDate = last(studyDate),'
        '          studyDescription = last(studyDescription),'
        '          issuerOfPatientId = last(issuerOfPatientId),'
        '          patientId = last(patientId),'
        '          patientName = last(patientName),'
        '          modality = last(modality),'
        '          instanceCount = count()) ~> allSeries'
        'existingSeries aggregate(groupBy(partitionName,'
        '          studyInstanceUid),'
        '     lastModifiedTimestamp = last(lastModifiedTimestamp),'
        '          studyDate = last(studyDate),'
        '          studyDescription = last(studyDescription),'
        '          issuerOfPatientId = last(issuerOfPatientId),'
        '          patientId = last(patientId),'
        '          patientName = last(patientName),'
        '          instanceCount = sum(instanceCount),'
        '          seriesCount = count()) ~> allStudies'
        'modifiedSeries alterRow(upsertIf(instanceCount>0),'
        '     deleteIf(instanceCount<=0)) ~> seriesSinkUpdates'
        'modifiedStudies alterRow(upsertIf(instanceCount>0),'
        '     deleteIf(instanceCount<=0)) ~> studySinkUpdate'
        'upToDate aggregate(groupBy(partitionName,'
        '          studyInstanceUid,'
        '          seriesInstanceUid),'
        '     instanceDifference = sum(iif(equals(action, \'Create\'), 1, iif(equals(action, \'Delete\'), -1, 0)))) ~> seriesChanges'
        'seriesChanges aggregate(groupBy(partitionName,'
        '          studyInstanceUid),'
        '     instanceDifference = sum(instanceDifference)) ~> studyChanges'
        'annotatedSeries filter(hasChange) ~> modifiedSeries'
        'allSeries derive(hasChange = not(isNull(seriesCache#lookup(partitionName, studyInstanceUid, seriesInstanceUid)))) ~> annotatedSeries'
        'allStudies derive(hasChange = not(isNull(studyCache#lookup(partitionName, studyInstanceUid)))) ~> annotatedStudies'
        'annotatedStudies filter(hasChange) ~> modifiedStudies'
        'aggregatedChanges filter(or(not(isNull(filePath)), equals(action, \'Delete\'))) ~> upToDate'
        'instanceSinkUpdates sink(allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     format: \'delta\','
        '     fileSystem: ($ContainerName),'
        '     folderPath: ($InstanceTablePath),'
        '     mergeSchema: true,'
        '     autoCompact: true,'
        '     optimizedWrite: false,'
        '     vacuum: ($RetentionHours),'
        '     deletable: true,'
        '     insertable: false,'
        '     updateable: false,'
        '     upsertable: true,'
        '     keys:[\'partitionName\',\'studyInstanceUid\',\'seriesInstanceUid\',\'sopInstanceUid\'],'
        '     umask: 0022,'
        '     preCommands: [],'
        '     postCommands: [],'
        '     saveOrder: 1,'
        '     mapColumn('
        '          partitionName,'
        '          studyInstanceUid,'
        '          seriesInstanceUid,'
        '          sopInstanceUid,'
        '          lastModifiedTimestamp,'
        '          studyDate,'
        '          studyDescription,'
        '          issuerOfPatientId,'
        '          patientId,'
        '          patientName,'
        '          modality,'
        '          sopClassUid,'
        '          filePath'
        '     ),'
        '     partitionBy(\'key\','
        '          0,'
        '          partitionName'
        '     )) ~> instanceTable'
        'seriesSinkUpdates sink(allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     format: \'delta\','
        '     fileSystem: ($ContainerName),'
        '     folderPath: ($SeriesTablePath),'
        '     mergeSchema: true,'
        '     autoCompact: true,'
        '     optimizedWrite: false,'
        '     vacuum: ($RetentionHours),'
        '     deletable: true,'
        '     insertable: false,'
        '     updateable: false,'
        '     upsertable: true,'
        '     keys:[\'partitionName\',\'studyInstanceUid\',\'seriesInstanceUid\'],'
        '     umask: 0022,'
        '     preCommands: [],'
        '     postCommands: [],'
        '     saveOrder: 2,'
        '     mapColumn('
        '          partitionName,'
        '          studyInstanceUid,'
        '          seriesInstanceUid,'
        '          lastModifiedTimestamp,'
        '          studyDate,'
        '          studyDescription,'
        '          issuerOfPatientId,'
        '          patientId,'
        '          patientName,'
        '          modality,'
        '          instanceCount'
        '     ),'
        '     partitionBy(\'key\','
        '          0,'
        '          partitionName'
        '     )) ~> seriesTable'
        'studySinkUpdate sink(allowSchemaDrift: true,'
        '     validateSchema: false,'
        '     format: \'delta\','
        '     fileSystem: ($ContainerName),'
        '     folderPath: ($StudyTablePath),'
        '     mergeSchema: true,'
        '     autoCompact: true,'
        '     optimizedWrite: false,'
        '     vacuum: ($RetentionHours),'
        '     deletable: true,'
        '     insertable: false,'
        '     updateable: false,'
        '     upsertable: true,'
        '     keys:[\'partitionName\',\'studyInstanceUid\'],'
        '     umask: 0022,'
        '     preCommands: [],'
        '     postCommands: [],'
        '     saveOrder: 3,'
        '     mapColumn('
        '          partitionName,'
        '          studyInstanceUid,'
        '          lastModifiedTimestamp,'
        '          studyDate,'
        '          studyDescription,'
        '          issuerOfPatientId,'
        '          patientId,'
        '          patientName,'
        '          seriesCount,'
        '          instanceCount'
        '     ),'
        '     partitionBy(\'key\','
        '          0,'
        '          partitionName'
        '     )) ~> studyTable'
        'seriesChanges sink(validateSchema: false,'
        '     keys:[\'partitionName\',\'studyInstanceUid\',\'seriesInstanceUid\'],'
        '     store: \'cache\','
        '     format: \'inline\','
        '     output: false,'
        '     saveOrder: 1) ~> seriesCache'
        'studyChanges sink(validateSchema: false,'
        '     keys:[\'partitionName\',\'studyInstanceUid\'],'
        '     store: \'cache\','
        '     format: \'inline\','
        '     output: false,'
        '     saveOrder: 1) ~> studyCache'
      ]
    }
  }
  dependsOn: [
    RestService
    AzureDataLakeStorage
  ]
}

module roleAssignmentFhirService './roleAssignment.bicep' = if (createRoleAssignment == true) {
  name: 'role-assign-fhir'
  scope: resourceGroup(resourceGroup().name)
  params: {
    fhirservicename: fhirservicename
    dicomservicename : dicomservicename
	  fhirContributorRoleAssignmentId: fhirContributorRoleAssignmentId
    dicomOwnerRoleAssignmentId: dicomOwnerRoleAssignmentId
    dicomReaderRoleAssignmentId:dicomReaderRoleAssignmentId
    principalId: principalId
    storageAccountName:storageAccountName
    storageBlobDataContributorRole:storageBlobDataContributorRole
    userPrincipalId:userPrincipalId
    dataFactoryName:dataFactoryName
    managedIdentityName:managedIdentityName
    keyVaultName:keyVaultName
  }
  dependsOn:[
    DICOM
    FHIR
    managedIdentity
    storageAccount
    dataFactory
    keyvault
  ]
}

