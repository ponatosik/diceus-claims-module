// Azure SQL: one logical server, two databases (main + Hangfire's separate store, ADR-0004), both on
// the free offer (100,000 vCore-seconds, 32 GB each per month) with AutoPause when the allowance is
// exhausted — they pause rather than bill (ADR-0012).
param location string
param serverName string
param adminLogin string
@secure()
param adminPassword string
@description('Key Vault that receives the two connection-string secrets.')
param keyVaultName string

resource server 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  properties: {
    administratorLogin: adminLogin
    administratorLoginPassword: adminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Lets App Service reach the server. The deploy pipeline adds (and removes) its own runner-IP rule
// only while applying migrations.
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: server
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

var databaseNames = ['ClaimsModule', 'ClaimsModuleHangfire']

resource databases 'Microsoft.Sql/servers/databases@2023-08-01-preview' = [for databaseName in databaseNames: {
  parent: server
  name: databaseName
  location: location
  sku: {
    name: 'GP_S_Gen5_2'
    tier: 'GeneralPurpose'
    family: 'Gen5'
    capacity: 2
  }
  properties: {
    useFreeLimit: true
    freeLimitExhaustionBehavior: 'AutoPause'
    autoPauseDelay: 60
    minCapacity: json('0.5')
    maxSizeBytes: 34359738368
    requestedBackupStorageRedundancy: 'Local'
    zoneRedundant: false
  }
}]

var connectionBase = 'Server=tcp:${server.properties.fullyQualifiedDomainName},1433;User ID=${adminLogin};Password=${adminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=60;'

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Written straight into Key Vault — never a deployment output.
resource claimsConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'ClaimsDbConnection'
  properties: {
    value: '${connectionBase}Initial Catalog=${databases[0].name};'
  }
}

resource hangfireConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'HangfireDbConnection'
  properties: {
    value: '${connectionBase}Initial Catalog=${databases[1].name};'
  }
}

output serverName string = server.name
output claimsDatabaseName string = databases[0].name
