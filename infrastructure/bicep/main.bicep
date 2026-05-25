targetScope = 'resourceGroup'

@description('Azure region for resources that require regional placement.')
param location string = 'centralus'

@description('Deployment environment name.')
@minLength(2)
param environmentName string = 'prod'

@description('Short lowercase app name used in Azure resource names.')
@minLength(2)
param appName string = 'dddsocial'

@description('Existing Azure Key Vault name.')
param keyVaultName string = 'kv-jdpeckham'

@description('Existing Azure DNS zone name.')
param dnsZoneName string = 'azure.jdpeckham.com'

@description('Custom host name for the Static Web App.')
param webHostName string = 'dddsocial.azure.jdpeckham.com'

@description('GitHub repository owner used for OIDC federation.')
param githubRepositoryOwner string = 'jpeckham'

@description('GitHub repository name used for OIDC federation.')
param githubRepositoryName string = 'domain-driven-design-csharp'

@description('GitHub branch allowed to deploy production.')
param githubBranch string = 'main'

@description('GitHub environment allowed to deploy production.')
param githubEnvironmentName string = 'production'

@description('Initial API container image. The deployment workflow updates this to the current ACR image.')
param apiContainerImage string = 'mcr.microsoft.com/dotnet/samples:aspnetapp'

@description('Container port exposed by SocialDDD.Api.')
param apiContainerPort int = 8080

@description('Cosmos DB Mongo database name.')
param cosmosDatabaseName string = 'socialddd'

@description('Existing shared free-tier Cosmos DB Mongo account name.')
param cosmosAccountName string = 'cosmos-cleansocial-prod'

@secure()
@description('JWT signing secret for SocialDDD.Api production authentication. Generated during infrastructure deployment unless explicitly overridden.')
param jwtSecret string = newGuid()

@description('Static Web Apps SKU. Free keeps MVP hosting cost minimized.')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSku string = 'Free'

@description('Create Azure RBAC role assignments. Use true for local bootstrap and false for GitHub redeployments because the GitHub identity intentionally cannot assign roles.')
param manageRoleAssignments bool = true

var suffix = '${appName}-${environmentName}'
var compactSuffix = take(toLower(replace(suffix, '-', '')), 16)
var acrName = 'acrdddsocialprod'
var tags = {
  application: appName
  environment: environmentName
  managedBy: 'bicep'
}
var githubRepo = '${githubRepositoryOwner}/${githubRepositoryName}'
var dnsRelativeRecordName = replace(webHostName, '.${dnsZoneName}', '')
var mediaShareName = 'media'
var mongoCollections = [
  'users'
  'posts'
  'follows'
  'blocks'
  'verification_codes'
  'remembered_devices'
  'device_otps'
  'password_reset_tokens'
]

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource dnsZone 'Microsoft.Network/dnsZones@2018-05-01' existing = {
  name: dnsZoneName
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = {
  name: cosmosAccountName
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2024-05-15' = {
  parent: cosmos
  name: cosmosDatabaseName
  properties: {
    resource: {
      id: cosmosDatabaseName
    }
    options: {
      throughput: 400
    }
  }
}

resource cosmosCollections 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases/collections@2024-05-15' = [for collectionName in mongoCollections: {
  parent: cosmosDatabase
  name: collectionName
  properties: {
    resource: {
      id: collectionName
      shardKey: {
        _id: 'Hash'
      }
      indexes: [
        {
          key: {
            keys: [
              '_id'
            ]
          }
        }
      ]
    }
    options: {}
  }
}]

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'st${compactSuffix}media'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    accessTier: 'Hot'
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource mediaShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: mediaShareName
  properties: {
    shareQuota: 10
    enabledProtocols: 'SMB'
  }
}

resource githubDeployIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-github-${suffix}'
  location: location
  tags: tags
}

resource apiIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-api-${suffix}'
  location: location
  tags: tags
}

resource githubBranchFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubDeployIdentity
  name: 'github-${githubBranch}'
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepo}:ref:refs/heads/${githubBranch}'
  }
}

resource githubEnvironmentFederatedCredential 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = {
  parent: githubDeployIdentity
  name: 'github-${githubEnvironmentName}'
  dependsOn: [
    githubBranchFederatedCredential
  ]
  properties: {
    audiences: [
      'api://AzureADTokenExchange'
    ]
    issuer: 'https://token.actions.githubusercontent.com'
    subject: 'repo:${githubRepo}:environment:${githubEnvironmentName}'
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${suffix}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource containerAppsMediaStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: containerAppsEnvironment
  name: 'media'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: mediaShare.name
      accessMode: 'ReadWrite'
    }
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: 'stapp-${suffix}'
  location: location
  tags: tags
  sku: {
    name: staticWebAppSku
    tier: staticWebAppSku
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Disabled'
  }
}

resource webCname 'Microsoft.Network/dnsZones/CNAME@2018-05-01' = {
  parent: dnsZone
  name: dnsRelativeRecordName
  properties: {
    TTL: 300
    CNAMERecord: {
      cname: staticWebApp.properties.defaultHostname
    }
  }
}

resource cosmosConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: '${appName}-${environmentName}-cosmos-mongo-connection-string'
  properties: {
    value: cosmos.listConnectionStrings().connectionStrings[0].connectionString
  }
}

resource jwtSecretValue 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: '${appName}-${environmentName}-jwt-secret'
  properties: {
    value: jwtSecret
  }
}

var keyVaultSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var acrPushRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8311e382-0749-4cb8-b61a-304f252e45ec')
var contributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c')

resource apiKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (manageRoleAssignments) {
  name: guid(keyVault.id, apiIdentity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: keyVaultSecretsUserRoleId
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiKeyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  parent: keyVault
  name: 'add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: apiIdentity.properties.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

resource apiAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (manageRoleAssignments) {
  name: guid(acr.id, apiIdentity.id, acrPullRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: apiIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource githubAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (manageRoleAssignments) {
  name: guid(acr.id, githubDeployIdentity.id, acrPushRoleId)
  scope: acr
  properties: {
    roleDefinitionId: acrPushRoleId
    principalId: githubDeployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource githubResourceGroupContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (manageRoleAssignments) {
  name: guid(resourceGroup().id, githubDeployIdentity.id, contributorRoleId)
  properties: {
    roleDefinitionId: contributorRoleId
    principalId: githubDeployIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'ca-api-${suffix}'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${apiIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: apiContainerPort
        transport: 'auto'
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      registries: [
        {
          server: acr.properties.loginServer
          identity: apiIdentity.id
        }
      ]
      secrets: [
        {
          name: 'mongo-connection-string'
          keyVaultUrl: cosmosConnectionSecret.properties.secretUri
          identity: apiIdentity.id
        }
        {
          name: 'jwt-secret'
          keyVaultUrl: jwtSecretValue.properties.secretUri
          identity: apiIdentity.id
        }
      ]
    }
    template: {
      scale: {
        minReplicas: 0
        maxReplicas: 3
      }
      volumes: [
        {
          name: 'media'
          storageType: 'AzureFile'
          storageName: containerAppsMediaStorage.name
        }
      ]
      containers: [
        {
          name: 'api'
          image: apiContainerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:${apiContainerPort}'
            }
            {
              name: 'Mongo__ConnectionString'
              secretRef: 'mongo-connection-string'
            }
            {
              name: 'Mongo__DatabaseName'
              value: cosmosDatabaseName
            }
            {
              name: 'Jwt__Secret'
              secretRef: 'jwt-secret'
            }
            {
              name: 'Jwt__Issuer'
              value: 'SocialDDD'
            }
            {
              name: 'Jwt__Audience'
              value: 'SocialDDD'
            }
            {
              name: 'Jwt__ExpiryMinutes'
              value: '60'
            }
            {
              name: 'Client__BaseUrl'
              value: 'https://${webHostName}'
            }
            {
              name: 'Features__EmailVerificationRepository'
              value: 'MongoDb'
            }
            {
              name: 'Features__OtpRepository'
              value: 'MongoDb'
            }
            {
              name: 'Features__RememberedDeviceRepository'
              value: 'MongoDb'
            }
            {
              name: 'Features__PasswordResetTokenRepository'
              value: 'MongoDb'
            }
            {
              name: 'Features__EmailService'
              value: 'Console'
            }
            {
              name: 'PostMedia__Directory'
              value: '/app/data/post-media'
            }
            {
              name: 'ProfileImages__Directory'
              value: '/app/data/profile-images'
            }
          ]
          volumeMounts: [
            {
              volumeName: 'media'
              mountPath: '/app/data'
            }
          ]
        }
      ]
    }
  }
  dependsOn: [
    apiKeyVaultAccessPolicy
  ]
}

output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output apiContainerAppName string = apiApp.name
output acrLoginServer string = acr.properties.loginServer
output acrName string = acr.name
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostName string = staticWebApp.properties.defaultHostname
output webHostName string = webHostName
output githubDeployClientId string = githubDeployIdentity.properties.clientId
output githubDeployPrincipalId string = githubDeployIdentity.properties.principalId
output mediaStorageAccountName string = storage.name
output cosmosAccountName string = cosmos.name
