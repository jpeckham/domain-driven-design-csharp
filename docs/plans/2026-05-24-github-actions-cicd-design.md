# GitHub Actions CI/CD Design

## Goal

Deploy `domain-driven-design-csharp` to Azure from GitHub Actions by reusing the deployment shape and Azure infrastructure components from `clean-architecture-csharp`.

## Design

The repository will use two workflows. Pull requests to `main` restore, build, test, and validate Docker Compose. Pushes to `main` and manual dispatches deploy production through GitHub OIDC, Azure Bicep, ACR, Azure Container Apps, and Azure Static Web Apps.

The Azure infrastructure is adapted from the reference repository with this app's names and runtime configuration: `dddsocial`, repository `domain-driven-design-csharp`, and hostname `dddsocial.azure.jdpeckham.com`. The deployment uses .NET 9, `SocialDDD.sln`, `Dockerfile.api`, and `src/SocialDDD.Client/SocialDDD.Client.csproj`.

The API keeps this repository's current supported production behavior. Cosmos DB provides the Mongo API connection. Bicep generates the production JWT signing secret and stores it in Key Vault. Mongo-backed repositories are enabled for production state. Email stays on the console implementation because `AzureCommunicationEmailService` is currently a stub. Media remains file-based and is mounted from an Azure Files share at `/app/data`.

## Testing

Verification will include `dotnet restore`, `dotnet build`, `dotnet test`, `docker compose config`, and Bicep build validation if Azure CLI/Bicep is available locally.
