param apiManagementServiceName string
param redisCacheHostName string
param redisCacheId string
param redisDatabaseId string
param redisApiVersion string
param redisPort int

var primaryKey = listKeys(redisDatabaseId, redisApiVersion).primaryKey

resource apimCache 'Microsoft.ApiManagement/service/caches@2022-08-01' = {
  name: '${apiManagementServiceName}/default'
  properties: {
    connectionString: '${redisCacheHostName}:${redisPort},password=${primaryKey},ssl=True,abortConnect=False'
    useFromLocation: 'default'
    // This must be HTTP Url
#disable-next-line use-resource-id-functions
    resourceId: '${environment().resourceManager}${redisCacheId}'
    description: redisCacheHostName
  }
}
