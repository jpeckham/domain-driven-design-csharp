param(
    [string]$ResourceGroupName = "rg-jdpeckham",
    [string]$TemplateFile = "infrastructure/bicep/main.bicep",
    [string]$ParametersFile = "infrastructure/bicep/parameters/prod.bicepparam",
    [string]$ApiContainerImage = "mcr.microsoft.com/dotnet/samples:aspnetapp"
)

$ErrorActionPreference = "Stop"

$deploymentName = "dddsocial-infra-$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss'))"

az deployment group create `
    --resource-group $ResourceGroupName `
    --name $deploymentName `
    --template-file $TemplateFile `
    --parameters $ParametersFile `
    --parameters apiContainerImage=$ApiContainerImage `
    --query "properties.outputs" `
    --output json
