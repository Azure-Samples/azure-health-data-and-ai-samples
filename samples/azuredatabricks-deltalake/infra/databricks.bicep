@description('Name of the Databricks workspace')
param workspaceName string

@description('Location for Azure Databricks workspack')
param location string

@allowed([
//  'standard'
  'premium'
])
@description('Tier for the Azure Databricks Service. Premium is required for Delta Live Tables.')
param tier string

@description('Resource tag for databricks')
param tags object = {}

@description('ID of the resource group for Databricks managed resources.')
param managedResourceGroupId string
param identity string
#disable-next-line secure-secrets-in-params
param adb_secret_scope_name string = 'sample-secrets'

param storageName string
param storageContainerName string = 'fhir'

resource databricks 'Microsoft.Databricks/workspaces@2022-04-01-preview' = {
  name: workspaceName
  location: location
  tags: tags
  sku: {
    name: tier
  }
  properties: {
    managedResourceGroupId: managedResourceGroupId
    publicNetworkAccess: 'Enabled'
  }
}

@description('Used to pull keys from existing deployment storage account')
resource deployStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageName
}

resource setupDatabricks 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'setupDatabricks'
  location: location
  kind: 'AzureCLI'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity}': {}
    }
  }
  properties: {
    azCliVersion: '2.26.0'
    containerSettings: {
      containerGroupName: 'setupDatabricks-${workspaceName}-ci'
    }
    storageAccountSettings: {
      storageAccountName: deployStorageAccount.name
      storageAccountKey: listKeys(deployStorageAccount.id, '2019-06-01').keys[0].value
    }
    timeout: 'PT10M'
    cleanupPreference: 'OnExpiration'
    retentionInterval: 'PT1H'
    environmentVariables: [
      {
        name: 'ADB_WORKSPACE_URL'
        value: databricks.properties.workspaceUrl
      }
      {
        name: 'ADB_WORKSPACE_ID'
        value: databricks.id
      }
      {
        name: 'SECRET_SCOPE_NAME'
        value: adb_secret_scope_name
      }
      {
        name: 'STORAGE_ACCOUNT_NAME'
        value: storageName
      }
      {
        name: 'RESOURCE_GROUP_NAME'
        value: resourceGroup().name
      }
      {
        name: 'STORAGE_CONTAINER_NAME'
        value: storageContainerName
      }
      {
        name: 'PIPELINE_TEMPLATE'
        value: loadTextContent('scripts/pipeline.json')
      }
    ]
    scriptContent: loadTextContent('scripts/setup-databricks.sh')
  }
}

output databricksWorkspaceId string = databricks.id
output databricksWorkspaceUrl string = databricks.properties.workspaceUrl
