// The Angular frontend on Static Web Apps Free (100 GB bandwidth included). Not linked to the repo:
// the deploy workflow uploads the build with the site's deployment token.
param location string
param name string

resource site 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {}
}

output name string = site.name
output defaultHostname string = site.properties.defaultHostname
