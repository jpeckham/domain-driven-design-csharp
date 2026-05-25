param(
    [string]$ResourceGroupName = "rg-jdpeckham",
    [string]$StaticWebAppName = "stapp-dddsocial-prod",
    [string]$HostName = "dddsocial.azure.jdpeckham.com",
    [string]$DnsZoneName = "azure.jdpeckham.com"
)

$ErrorActionPreference = "Stop"

az staticwebapp hostname set `
    --resource-group $ResourceGroupName `
    --name $StaticWebAppName `
    --hostname $HostName `
    --validation-method "dns-txt-token" `
    --no-wait

$customDomain = $null
for ($attempt = 1; $attempt -le 30; $attempt++) {
    $customDomainJson = az staticwebapp hostname show `
        --resource-group $ResourceGroupName `
        --name $StaticWebAppName `
        --hostname $HostName `
        --output json 2>$null

    if ($customDomainJson) {
        $customDomain = $customDomainJson | ConvertFrom-Json
        if ($customDomain.validationToken -or $customDomain.status -eq "Ready") {
            break
        }
    }

    Start-Sleep -Seconds 10
}

if ($null -eq $customDomain) {
    throw "Static Web App custom domain '$HostName' was not created."
}

$relativeName = $HostName.Replace(".$DnsZoneName", "")
$txtRecordSetName = if ($relativeName -eq $HostName) { "_dnsauth" } else { "_dnsauth.$relativeName" }

if ($customDomain.validationToken) {
    az network dns record-set txt create `
        --resource-group $ResourceGroupName `
        --zone-name $DnsZoneName `
        --name $txtRecordSetName `
        --ttl 300 `
        --output none

    $txtRecordSet = az network dns record-set txt show `
        --resource-group $ResourceGroupName `
        --zone-name $DnsZoneName `
        --name $txtRecordSetName `
        --output json | ConvertFrom-Json

    $existingValues = @($txtRecordSet.TXTRecords | ForEach-Object { $_.value } | ForEach-Object { $_ })
    if ($existingValues -notcontains $customDomain.validationToken) {
        az network dns record-set txt add-record `
            --resource-group $ResourceGroupName `
            --zone-name $DnsZoneName `
            --record-set-name $txtRecordSetName `
            --value $customDomain.validationToken `
            --output none
    }
}
elseif ($customDomain.status -ne "Ready") {
    throw "Static Web App custom domain '$HostName' did not return a validation token."
}

for ($attempt = 1; $attempt -le 30; $attempt++) {
    $customDomain = az staticwebapp hostname show `
        --resource-group $ResourceGroupName `
        --name $StaticWebAppName `
        --hostname $HostName `
        --output json | ConvertFrom-Json

    if ($customDomain.status -eq "Ready") {
        break
    }

    Start-Sleep -Seconds 10
}

az staticwebapp hostname show `
    --resource-group $ResourceGroupName `
    --name $StaticWebAppName `
    --hostname $HostName `
    --output table
