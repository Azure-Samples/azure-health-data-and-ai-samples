@description('Location of all resources')
param location string

param apiManagementServiceName string

resource redisCache 'Microsoft.Cache/redisEnterprise@2025-04-01' = {
  name: '${apiManagementServiceName}-cache'
  location: location
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    encryption: {}
    highAvailability: 'Disabled'
    minimumTlsVersion: '1.2'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-04-01' = {
  name: 'default'
  parent: redisCache
  properties: {
    accessKeysAuthentication: 'Enabled'
    clientProtocol: 'Encrypted'
    clusteringPolicy: 'OSSCluster'
    evictionPolicy: 'VolatileLRU'
    modules: []
    port: 10000
  }
}

output redisApiVersion string = redisCache.apiVersion
output redisCacheHostName string = redisCache.properties.hostName
output redisCacheId string = redisCache.id
output redisDatabaseId string = redisDatabase.id
output redisPort int = redisDatabase.properties.port
