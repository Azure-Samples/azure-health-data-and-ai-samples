@description('Log Analytics workspack name')
param workspaceName string
@description('Location to deploy the Log Analytics workspack')
param location string
@description('Resource tags for the Log Analytics workspack')
param tags object = {}

param sku string = 'PerGB2018'

@description('Specify the number of days to retain data.')
param retentionInDays int = 120

resource workspace 'Microsoft.OperationalInsights/workspaces@2021-12-01-preview' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: sku
    }
    retentionInDays: retentionInDays
  }
}

output loagAnalyticsId string = workspace.id
