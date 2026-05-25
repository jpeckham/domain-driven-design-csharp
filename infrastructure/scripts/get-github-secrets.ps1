param(
    [string]$ResourceGroupName = "rg-jdpeckham"
)

$ErrorActionPreference = "Stop"

$clientId = az identity show `
    --resource-group $ResourceGroupName `
    --name "id-github-dddsocial-prod" `
    --query clientId `
    --output tsv

@"
AZURE_CLIENT_ID=$clientId
AZURE_TENANT_ID=7f4e14a4-417c-496f-82d9-9fb7940c3d17
AZURE_SUBSCRIPTION_ID=6d88cea2-aec5-4d58-88c4-4830a867b3cd
"@
