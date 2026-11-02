// Document storage (§13, BR-D-01): Standard LRS, Hot, private, no geo-replication or soft-delete —
// nothing that adds cost beyond a few cents for a handful of documents.
param location string
param name string
@description('Key Vault that receives the BlobConnection secret.')
param keyVaultName string

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: name
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: false
    }
    containerDeleteRetentionPolicy: {
      enabled: false
    }
    isVersioningEnabled: false
  }
}

// AzureBlobStorageService also creates it on first upload; declared here so the deployment is complete.
resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'claim-documents'
  properties: {
    publicAccess: 'None'
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

// Account-key connection string: the API signs 1-hour SAS URLs with it (ADR-0012). Written straight
// into Key Vault — never a deployment output.
resource blobConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: vault
  name: 'BlobConnection'
  properties: {
    value: 'DefaultEndpointsProtocol=https;AccountName=${account.name};AccountKey=${account.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
  }
}

output name string = account.name
