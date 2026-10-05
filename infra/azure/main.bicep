targetScope = 'resourceGroup'

@description('Azure region for the Container Apps resources.')
param location string = resourceGroup().location

@description('Prefix used for globally scoped and resource-group scoped names.')
@minLength(3)
@maxLength(20)
param namePrefix string = 'qualityworkflow'

@description('OCI image containing the ASP.NET Core API.')
param containerImage string

@secure()
@description('SQL Server connection string supplied through a Container App secret.')
param sqlConnectionString string

@description('Optional Azure OpenAI endpoint. Leave blank to use deterministic local review rules.')
param azureOpenAiEndpoint string = ''

@description('Optional Azure OpenAI deployment name.')
param azureOpenAiDeployment string = ''

@secure()
@description('Optional Azure OpenAI API key. Managed identity should replace keys after role assignment is designed.')
param azureOpenAiApiKey string = ''

@minValue(0)
@maxValue(10)
param minReplicas int = 1

@minValue(1)
@maxValue(20)
param maxReplicas int = 3

var aiEnabled = !empty(azureOpenAiEndpoint) && !empty(azureOpenAiDeployment) && !empty(azureOpenAiApiKey)
var workspaceName = '${namePrefix}-logs'
var environmentName = '${namePrefix}-env'
var appName = '${namePrefix}-api'

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: environmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
      }
    }
  }
}

resource app 'Microsoft.App/containerApps@2024-03-01' = {
  name: appName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
      }
      secrets: concat([
        {
          name: 'sql-connection'
          value: sqlConnectionString
        }
      ], aiEnabled ? [
        {
          name: 'azure-openai-key'
          value: azureOpenAiApiKey
        }
      ] : [])
    }
    template: {
      containers: [
        {
          name: 'api'
          image: containerImage
          env: concat([
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'DatabaseProvider'
              value: 'SqlServer'
            }
            {
              name: 'ConnectionStrings__QualityWorkflow'
              secretRef: 'sql-connection'
            }
            {
              name: 'AiReview__Provider'
              value: aiEnabled ? 'AzureOpenAI' : 'Deterministic'
            }
          ], aiEnabled ? [
            {
              name: 'AiReview__Endpoint'
              value: azureOpenAiEndpoint
            }
            {
              name: 'AiReview__Deployment'
              value: azureOpenAiDeployment
            }
            {
              name: 'AiReview__ApiKey'
              secretRef: 'azure-openai-key'
            }
          ] : [])
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 5
              periodSeconds: 10
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output apiFqdn string = app.properties.configuration.ingress.fqdn
output managedIdentityPrincipalId string = app.identity.principalId
