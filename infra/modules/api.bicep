// The API on App Service F1 Free (Linux, .NET 9). No Always On on F1: the app sleeps after ~20 idle
// minutes and Hangfire only runs while it is awake — accepted in ADR-0012. Secrets come from Key Vault
// references resolved with the site's system-assigned identity.
param location string
param planName string
param siteName string
param keyVaultName string
param frontendOrigin string
param jwtAudience string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  properties: {
    reserved: true // Linux
  }
}

func kvRef(vaultName string, secretName string) string => '@Microsoft.KeyVault(VaultName=${vaultName};SecretName=${secretName})'

resource site 'Microsoft.Web/sites@2023-12-01' = {
  name: siteName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    keyVaultReferenceIdentity: 'SystemAssigned'
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      alwaysOn: false // not available on F1
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      http20Enabled: true
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConnectionStrings__ClaimsDatabase', value: kvRef(keyVaultName, 'ClaimsDbConnection') }
        { name: 'ConnectionStrings__HangfireDatabase', value: kvRef(keyVaultName, 'HangfireDbConnection') }
        { name: 'ConnectionStrings__AzureBlobStorage', value: kvRef(keyVaultName, 'BlobConnection') }
        { name: 'StorageProvider', value: 'AzureBlob' }
        // Program.cs always prepares the local-storage folder; /home is App Service's writable share.
        { name: 'LocalStorage__RootPath', value: '/home/uploads' }
        { name: 'Jwt__Issuer', value: 'https://${siteName}.azurewebsites.net' }
        { name: 'Jwt__Audience', value: jwtAudience }
        { name: 'Jwt__SigningKey', value: kvRef(keyVaultName, 'JwtSigningKey') }
        // ADR-0010: the demo deployment's three fixed Demo Users are its test credentials (§20.3).
        { name: 'DevAuth__Enabled', value: 'true' }
        { name: 'Cors__AllowedOrigins__0', value: frontendOrigin }
      ]
    }
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Key Vault Secrets User: read secret values, nothing else.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource apiReadsSecrets 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(vault.id, site.id, keyVaultSecretsUserRoleId)
  scope: vault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output siteName string = site.name
output defaultHostname string = site.properties.defaultHostName
