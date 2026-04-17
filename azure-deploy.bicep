// Azure Bicep — deploys App Service Plan + Web App + SQL Server + SQL DB
// Run: az deployment group create --resource-group rg-fleetmanager --template-file azure-deploy.bicep

@description('Base name used for all resources')
param appName string = 'fleetmanager'

@description('Azure region')
param location string = resourceGroup().location

@description('SQL Server admin login')
param sqlAdminLogin string = 'sqladmin'

@secure()
@description('SQL Server admin password')
param sqlAdminPassword string

// ── App Service Plan ──────────────────────────────────────────────────────────
resource appPlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: '${appName}-plan'
  location: location
  sku: {
    name: 'B1'     // Basic – swap to S1/P1 for production
    tier: 'Basic'
  }
  properties: {
    reserved: false
  }
}

// ── Web App ────────────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appName}-app'
  location: location
  properties: {
    serverFarmId: appPlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      appSettings: [
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Stripe__PublishableKey',  value: 'pk_live_REPLACE_ME' }
        { name: 'Stripe__SecretKey',       value: 'sk_live_REPLACE_ME' }
        { name: 'GoogleMaps__ApiKey',      value: 'REPLACE_ME' }
      ]
      connectionStrings: [
        {
          name: 'DefaultConnection'
          connectionString: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDb.name};User ID=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=true;'
          type: 'SQLAzure'
        }
      ]
    }
    httpsOnly: true
  }
}

// ── SQL Server ────────────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-02-01-preview' = {
  name: '${appName}-sql'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
  }
}

// Allow Azure services to connect
resource sqlFirewall 'Microsoft.Sql/servers/firewallRules@2023-02-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress:   '0.0.0.0'
  }
}

// ── SQL Database ──────────────────────────────────────────────────────────────
resource sqlDb 'Microsoft.Sql/servers/databases@2023-02-01-preview' = {
  parent: sqlServer
  name: 'FleetManagerDb'
  location: location
  sku: {
    name:     'Basic'
    tier:     'Basic'
    capacity: 5
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
