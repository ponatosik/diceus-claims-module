// Azure resources for the Claims module — spec 013, ADR-0012.
// Every resource is pinned to a free or capped setting so a $2/month budget on a Pay-As-You-Go
// subscription is never at risk. Deployed by .github/workflows/provision.yml (deployment name "main").
targetScope = 'resourceGroup'

@description('Azure region for every resource (Static Web Apps supports a limited set; westeurope is one).')
param location string = resourceGroup().location

@description('Short name used in every resource name.')
param appName string = 'diceus-claims'

@description('Email address that receives the monthly budget alerts.')
param budgetAlertEmail string

@description('Monthly budget in USD for the whole resource group.')
param monthlyBudget int = 2

@description('First day of the budget period (yyyy-MM-01). Must not change after the first deployment.')
param budgetStartDate string = '2026-11-01'

@description('SQL Server admin password — generated once by the provision workflow and kept in Key Vault.')
@secure()
param sqlAdminPassword string

@description('HMAC signing key for the demo JWT issuer (ADR-0007/0010) — generated once, kept in Key Vault.')
@secure()
param jwtSigningKey string

var suffix = take(uniqueString(resourceGroup().id), 6)
var names = {
  plan: 'asp-${appName}'
  api: 'app-${appName}-api-${suffix}'
  staticWebApp: 'stapp-${appName}'
  sqlServer: 'sql-${appName}-${suffix}'
  storage: 'st${replace(appName, '-', '')}${suffix}'
  keyVault: 'kv-${take(replace(appName, '-', ''), 10)}-${suffix}'
}
var sqlAdminLogin = 'claimsadmin'
var jwtAudience = 'claims-module-api'

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    name: names.keyVault
    sqlAdminPassword: sqlAdminPassword
    jwtSigningKey: jwtSigningKey
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    name: names.storage
    keyVaultName: keyVault.outputs.name
  }
}

module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    serverName: names.sqlServer
    adminLogin: sqlAdminLogin
    adminPassword: sqlAdminPassword
    keyVaultName: keyVault.outputs.name
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'staticWebApp'
  params: {
    location: location
    name: names.staticWebApp
  }
}

module api 'modules/api.bicep' = {
  name: 'api'
  params: {
    location: location
    planName: names.plan
    siteName: names.api
    keyVaultName: keyVault.outputs.name
    frontendOrigin: 'https://${staticWebApp.outputs.defaultHostname}'
    jwtAudience: jwtAudience
  }
  // The API's Key Vault references must find their secrets on first start.
  dependsOn: [
    storage
    sql
  ]
}

module budget 'modules/budget.bicep' = {
  name: 'budget'
  params: {
    name: 'budget-${appName}'
    amount: monthlyBudget
    startDate: budgetStartDate
    alertEmail: budgetAlertEmail
  }
}

output apiName string = api.outputs.siteName
output apiUrl string = 'https://${api.outputs.defaultHostname}'
output staticWebAppName string = staticWebApp.outputs.name
output frontendUrl string = 'https://${staticWebApp.outputs.defaultHostname}'
output sqlServerName string = sql.outputs.serverName
output keyVaultName string = keyVault.outputs.name
