// Every secret lives here (ADR-0012), read by the API through Key Vault references and its managed
// identity. RBAC authorisation; Standard tier (cents per 10,000 operations). The other modules write
// their own secrets into this vault, so no secret value ever appears in a deployment output.
param location string
param name string
@secure()
param sqlAdminPassword string
@secure()
param jwtSigningKey string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: name
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'SqlAdminPassword'
  properties: {
    value: sqlAdminPassword
  }
}

resource jwtKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'JwtSigningKey'
  properties: {
    value: jwtSigningKey
  }
}

output name string = vault.name
