@description('Prefix for resources deployed by this solution (App Service, Function App, monitoring, etc)')
param prefixName string = 'hdssdk${uniqueString(resourceGroup().id)}'

@description('Do you want to create a new Azure Health Data Services workspace or use an existing one?')
param createWorkspace bool

@description('Do you want to create a new FHIR Service or use an existing one?')
param createFhirService bool

@description('Name of Azure Health Data Services workspace to deploy or use.')
param workspaceName string

@description('Name of the FHIR service to deloy or use.')
param fhirServiceName string

@description('Name of the Log Analytics workspace to deploy or use. Leave blank to skip deployment')
param logAnalyticsName string = '${prefixName}-la'

@description('Location to deploy resources')
param location string

@description('Location to deploy resources')
param appTags object = {}

@description('ID of principals to give FHIR Contributor on the FHIR service')
param fhirContributorPrincipals array = []

@description('Any custom function app settings')
param functionAppCustomSettings object = {}

@description('Tenant ID where resources are deployed')
var tenantId  = subscription().tenantId

param hl7continername string
param name string

@description('Deploy Azure Health Data Services and FHIR service')
module fhir './fhir.bicep'= {
  name: 'fhirDeploy'
  params: {
    createWorkspace: createWorkspace
    createFhirService: createFhirService
    workspaceName: workspaceName
    fhirServiceName: fhirServiceName
    location: location
    tenantId: tenantId
    appTags: appTags
  }
}

@description('Name of the storage account')
var storagename ='${replace(prefixName, '-', '')}funcsa'


@description('Create Storage Account')
module storage './storage.bicep'= {
  name: 'storageDeploy'
  params: {
    storageAccountName:storagename
    location:location
    hl7continername : hl7continername
    appTags: appTags
  }
}

var storagekey=storage.outputs.accountkey

@description('Name for app insights resource used to monitor the Function App')
var appInsightsName = '${prefixName}-appins'

@description('Deploy monitoring and logging')
module monitoring './monitoring.bicep'= {
  name: 'monitoringDeploy'
  params: {
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
    location: location    
    appTags: appTags
  }
}

@description('Name for the App Service used to host the Function App.')
var appServiceName = '${prefixName}-appserv'


@description('Deploy Azure Function to validate the hl7 files')
module AzureFunc './AzureFunction.bicep'= {
  name: 'validatefunctionDeploy'
  params: {
    appServiceName: appServiceName
    functionAppName: 'dataingestion20220512'
    storageAccountName: storagename
    storagekey:storagekey
    location: location
    hl7FilesContinerName: storage.outputs.hl7filescontainer
    hl7ValidatedBlobContainer : storage.outputs.validatedcontainer
    hl7validationfailContainer: storage.outputs.hl7failedvalidationfilescontainer
    hl7resyncontainer: storage.outputs.hl7resyncontainers
    hl7ConvertedContainer: storage.outputs.hl7convertedfilescontainer
    hl7conversionfailcontainer:storage.outputs.hl7Failedcontainer
    hl7SkippedBlobContainer: storage.outputs.hl7skippedfilecontainer
    hl7SuccessBlobContainer: storage.outputs.hl7processedfilescontainer
    hl7FailedBlobContainer:  storage.outputs.hl7failedfilescontainer
    appInsightsInstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    functionSettings: union({
      AZURE_FhirServerUrl: 'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com'
      AZURE_InstrumentationKey: monitoring.outputs.appInsightsInstrumentationKey
    }, functionAppCustomSettings)
    appTags: appTags
  }
}

@description('Deploy Logic App')
module logicapp 'logicapp.bicep'= {
  name: 'logicappDeploy'
  params: {   
    location: location    
    appTags: appTags   
    name: name
    key: AzureFunc.outputs.functionkey
    funcAppName: AzureFunc.outputs.functionAppName
    funcURL : AzureFunc.outputs.functionURL
    storageAccountName: storagename
    storagekey:storagekey
  }
  
}
