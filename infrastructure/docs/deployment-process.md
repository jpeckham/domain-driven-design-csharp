# Azure Deployment Process

## Prerequisites

- Azure CLI authenticated to tenant `7f4e14a4-417c-496f-82d9-9fb7940c3d17`
- Subscription `6d88cea2-aec5-4d58-88c4-4830a867b3cd`
- Existing resource group `rg-jdpeckham`
- Existing DNS zone `azure.jdpeckham.com`
- Existing Key Vault `kv-jdpeckham`
- Permission to create role assignments in `rg-jdpeckham`
- Existing shared free-tier Cosmos DB Mongo account `cosmos-cleansocial-prod`

## First Infrastructure Deployment

```powershell
./infrastructure/scripts/deploy-infrastructure.ps1
```

The first deployment creates Azure resources and uses a public ASP.NET sample image as a bootstrap Container Apps image. The GitHub production workflow replaces it with the real API image.

## Configure Static Web Apps Domain

```powershell
./infrastructure/scripts/configure-static-web-domain.ps1
```

This binds `dddsocial.azure.jdpeckham.com` to the Static Web App and enables the managed HTTPS certificate.

## Configure GitHub Secrets

Use the Bicep output `githubDeployClientId`:

```powershell
./infrastructure/scripts/get-github-secrets.ps1
```

Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` in the GitHub `production` environment. The production deployment workflow reads the Azure Static Web Apps deployment token after infrastructure deployment.

## Production Deployment

The `.github/workflows/production-deploy.yml` workflow:

1. Logs into Azure using GitHub OIDC.
2. Re-applies Bicep for idempotent infrastructure drift correction.
3. Runs the test suite.
4. Pushes the API image to ACR.
5. Updates the Container App image.
6. Writes the Blazor `appsettings.json` with the deployed API URL.
7. Builds the Blazor frontend.
8. Deploys the frontend to Azure Static Web Apps.

## Pull Request Validation

The `.github/workflows/pull-request-validation.yml` workflow restores, builds, tests, and validates Docker Compose configuration without deploying Azure resources.
