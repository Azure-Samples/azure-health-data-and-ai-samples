param staticWebAppName string
param location string
param appTags object = {}
param enableVNetSupport bool
param sku object = enableVNetSupport ? {
  name: 'Standard'
  tier: 'Standard'
} : {
  name: 'Free'
  tier: 'Free'
}

var allowedRegions = ['centralus', 'eastus2', 'westus2']
var modifiedLocatin = contains(allowedRegions, location) ? location : 'centralus'

resource web 'Microsoft.Web/staticSites@2022-03-01' = {
  name: staticWebAppName
  location: modifiedLocatin
  tags: appTags
  sku: sku
  properties: {
    provider: 'Custom'
  }
}

output name string = web.name
output uri string = 'https://${web.properties.defaultHostname}'
