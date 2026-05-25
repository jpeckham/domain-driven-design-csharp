# GitHub Production Secrets

Configure these secrets for the `production` GitHub environment.

```text
AZURE_CLIENT_ID=Use the githubDeployClientId Bicep output
AZURE_TENANT_ID=7f4e14a4-417c-496f-82d9-9fb7940c3d17
AZURE_SUBSCRIPTION_ID=6d88cea2-aec5-4d58-88c4-4830a867b3cd
```

`AZURE_CLIENT_ID` uses GitHub OIDC through the user-assigned identity created by Bicep. The production deployment workflow reads the Azure Static Web Apps deployment token after the infrastructure deployment creates or updates the Static Web App.
