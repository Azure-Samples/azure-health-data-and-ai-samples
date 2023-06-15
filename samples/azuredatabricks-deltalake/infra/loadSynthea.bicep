param name string
param fhirUrl string
param location string
param identity string
param storageName string

param utcValue string = utcNow()

@description('Used to pull keys from existing deployment storage account')
resource deployStorageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' existing = {
  name: storageName
}

var storageKey = listKeys(deployStorageAccount.id, '2019-06-01').keys[0].value

@description('Deploymenet script to load sample Synthea data')
resource loadSyntheaData 'Microsoft.Resources/deploymentScripts@2020-10-01' = {
  name: 'loadSyntheaData'
  location: location
  kind: 'AzurePowerShell'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity}': {}
    }
  }
  properties: {
    azPowerShellVersion: '9.7'
    forceUpdateTag: utcValue
    containerSettings: {
      containerGroupName: 'loadSyntheaData-${name}-ci'
    }
    storageAccountSettings: {
      storageAccountName: deployStorageAccount.name
      storageAccountKey: storageKey
    }
    timeout: 'PT2H'
    cleanupPreference: 'OnExpiration'
    retentionInterval: 'PT1H'
    environmentVariables: [
      {
        name: 'FHIR_URL'
        value: fhirUrl
      }
      {
        name: 'STORAGE_ACCOUNT_NAME'
        value: deployStorageAccount.name
      }
      {
        name: 'MSI'
        value: identity
      }
    ]
    scriptContent: loadTextContent('scripts/load-synthea-data.ps1')
  }
}
