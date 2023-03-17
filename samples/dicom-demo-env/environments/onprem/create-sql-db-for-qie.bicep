// This script deploys the on-prem solution 

@description('The name of the SQL logical server.')
param serverName string = uniqueString('sql', resourceGroup().id)

@description('The name of the SQL Database.')
param sqlDBName string = 'qie'

@description('Location for all resources.')
param location string = resourceGroup().location

@description('The administrator username of the SQL logical server.')
param administratorLogin string = 'student'

@description('The administrator password of the SQL logical server.')
@secure()
param administratorLoginPassword string

resource sqlServer 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDB 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  parent: sqlServer
  name: sqlDBName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
     
  }
}

// Using 0.0.0.0 as both start and end IP addresses turns on the "Allow Azure services to access this server"
resource firewalls 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  name: 'allow_azure_services'
  parent: sqlServer
  properties: {
    endIpAddress: '0.0.0.0'
    startIpAddress: '0.0.0.0'
  }
}


output sqlServerName string = sqlServer.name
output sqlServerId string = sqlServer.id
output dbName string = sqlDB.name
output dbId string = sqlDB.id
