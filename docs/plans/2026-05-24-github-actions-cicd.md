# GitHub Actions CI/CD Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add GitHub Actions CI/CD that validates pull requests and deploys production Azure infrastructure, API, and Blazor client for SocialDDD.

**Architecture:** Copy the proven workflow and Bicep structure from `clean-architecture-csharp`, then adapt names, project paths, Dockerfiles, .NET version, and runtime configuration keys for this repository. Production uses Azure Container Apps for the API, Azure Static Web Apps for the Blazor client, ACR for images, Cosmos DB Mongo API for data, Azure Files for media persistence, and GitHub OIDC for deployment authentication.

**Tech Stack:** GitHub Actions, Azure CLI, Azure Bicep, Azure Container Apps, Azure Container Registry, Azure Static Web Apps, Cosmos DB Mongo API, Azure Files, .NET 9, Docker.

---

### Task 1: Add Pull Request Validation Workflow

**Files:**
- Create: `.github/workflows/pull-request-validation.yml`

**Steps:**
1. Create workflow triggered by pull requests to `main`.
2. Use `actions/checkout@v4` and `actions/setup-dotnet@v4` with `9.0.x`.
3. Run `dotnet restore SocialDDD.sln`.
4. Run `dotnet build SocialDDD.sln --configuration Release --no-restore`.
5. Run `dotnet test SocialDDD.sln --configuration Release --no-build`.
6. Run `docker compose config`.

### Task 2: Add Production Deployment Workflow

**Files:**
- Create: `.github/workflows/production-deploy.yml`

**Steps:**
1. Trigger on `workflow_dispatch` and pushes to `main`.
2. Configure `contents: read` and `id-token: write`.
3. Validate `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID`.
4. Log in with `azure/login@v2`.
5. Deploy Bicep with `manageRoleAssignments=false`.
6. Extract ACR, Container App, API URL, and Static Web App outputs.
7. Fetch the Static Web App deployment token from Azure.
8. Run `dotnet test SocialDDD.sln --configuration Release`.
9. Build and push `Dockerfile.api` to ACR.
10. Update the API Container App image.
11. Write `src/SocialDDD.Client/wwwroot/appsettings.json` with `ApiBaseUrl`.
12. Publish the Blazor client and deploy it to Static Web Apps.

### Task 3: Add Adapted Azure Bicep Infrastructure

**Files:**
- Create: `infrastructure/bicep/main.bicep`
- Create: `infrastructure/bicep/parameters/prod.bicepparam`

**Steps:**
1. Define production resources for ACR, Cosmos Mongo, Storage, Azure Files, Container Apps, Static Web Apps, DNS, and GitHub OIDC identity.
2. Use `appName = dddsocial`, `githubRepositoryName = domain-driven-design-csharp`, and `webHostName = dddsocial.azure.jdpeckham.com`.
3. Generate the production JWT secret through Bicep and store it in Key Vault.
4. Configure API environment variables using this repository's keys: `Mongo__*`, `Jwt__*`, `Features__*`, `Client__BaseUrl`, `PostMedia__Directory`, and `ProfileImages__Directory`.
5. Keep email configured as `Console`.

### Task 4: Add Deployment Documentation and Helper Scripts

**Files:**
- Create: `infrastructure/github/production-secrets.example.md`
- Create: `infrastructure/scripts/deploy-infrastructure.ps1`
- Create: `infrastructure/scripts/get-github-secrets.ps1`
- Create: `infrastructure/scripts/configure-static-web-domain.ps1`
- Create: `infrastructure/docs/deployment-process.md`

**Steps:**
1. Adapt scripts from the reference repository to `dddsocial`.
2. Document first deployment, custom domain binding, GitHub environment secrets, and production workflow behavior.

### Task 5: Verify

**Commands:**
1. `dotnet restore SocialDDD.sln`
2. `dotnet build SocialDDD.sln --configuration Release --no-restore`
3. `dotnet test SocialDDD.sln --configuration Release --no-build`
4. `docker compose config`
5. `az bicep build --file infrastructure/bicep/main.bicep`
