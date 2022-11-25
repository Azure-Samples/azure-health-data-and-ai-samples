targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment which is used to generate a short unique hash used in resources.')
param name string

@description('Data lake Storage Account Name.')
param dataLakeName string = ''

@description('Data lake Storage FileSystem Name.')
param dataLakefileSystemName string = ''

@description('Synapse Workspace Name')
param synapseworkspaceName string = ''

@description('SQL server admin login username.')
param sqlAdministratorLogin string = ''

@secure()
@description('SQL server admin login password.')
param sqlAdministratorLoginPassword string = ''

@minLength(1)
@description('Primary location for all resources')
param location string = ''

@description('Allow all connection for synapse workspace firewall.')
param allowAllConnections bool = true

@description('Name of your existing resource group (leave blank to create a new one)')
param existingResourceGroupName string = ''

var createResourceGroup = empty(existingResourceGroupName) ? true : false

var appTags = {
  'PowerBI sample': 'powerbi-analytics-pipeline-sample'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' = if (createResourceGroup) {
  name: '${name}-rg'
  location: location
  tags: appTags
}

resource existingResourceGroup 'Microsoft.Resources/resourceGroups@2021-04-01' existing = if (!createResourceGroup) {
  name: existingResourceGroupName
}

module template 'core.bicep'= if (createResourceGroup) {
  name: 'main'
  scope: resourceGroup
  params: {
    storageAccountName: dataLakeName
    filesystemName : dataLakefileSystemName
    location: location
    appTags: appTags
    allowAllConnections : allowAllConnections
    synapseworkspaceName : synapseworkspaceName
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorLoginPassword: sqlAdministratorLoginPassword
  }
}

module existingResourceGrouptemplate 'core.bicep'= if (!createResourceGroup) {
  name: 'mainExistingResourceGroup'
  scope: existingResourceGroup
  params: {
    storageAccountName: dataLakeName
    filesystemName : dataLakefileSystemName
    location: location
    appTags: appTags
    allowAllConnections : allowAllConnections
    synapseworkspaceName : synapseworkspaceName
    sqlAdministratorLogin: sqlAdministratorLogin
    sqlAdministratorLoginPassword: sqlAdministratorLoginPassword
  }
}

output synapseworkspacename string = createResourceGroup ? template.outputs.synapseworkspacename : existingResourceGrouptemplate.outputs.synapseworkspacename
output dataLakeName string = createResourceGroup ? template.outputs.dataLakeName : existingResourceGrouptemplate.outputs.dataLakeName
