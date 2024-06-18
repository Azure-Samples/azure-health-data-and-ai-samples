param name string
param nameCleanShort string
param location string
param appTags object


@description('Name for the storage account needed for Custom Operation Function Apps')
var customOperationsFuncStorName = '${nameCleanShort}funcsa'

@description('Used for Custom Operation Azure Function App temp storage and auth.')
resource funcStorageAccount 'Microsoft.Storage/storageAccounts@2021-08-01' = {
  name: customOperationsFuncStorName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  tags: appTags
}

@description('Name for the App Service used to host Custom Operation Function Apps.')
var customOperationsAppServiceName = '${name}-appserv'

@description('App Service used to run Azure Function')
resource hostingPlan 'Microsoft.Web/serverfarms@2021-03-01' = {
  name: customOperationsAppServiceName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    // reserved: true
  }
  tags: appTags
}

output storageAccountName string = customOperationsFuncStorName
output hostingPlanName string = customOperationsAppServiceName
output hostingPlanId string = hostingPlan.id
