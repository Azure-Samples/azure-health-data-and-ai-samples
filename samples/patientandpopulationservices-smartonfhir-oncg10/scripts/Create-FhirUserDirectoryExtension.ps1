<#
    Creates a fhirUser directory extension in Microsoft Entra ID. This is required for the FHIR Server to work with Microsoft Entra ID authentication.
#>
param (
    [Parameter(Mandatory=$false)]
    [string]$FhirResourceAppId
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName
$ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
Write-Host "Using Azure Account logged in with the Azure CLI: $($ACCOUNT.name) - $($ACCOUNT.id)"


if ([string]::IsNullOrWhiteSpace($FhirResourceAppId)) {

    Write-Host "FhirResourceAppId parameter blank, looking in azd enviornment configuration...."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FhirResourceAppId) -and $name -eq "FhirResourceAppId") {
            $FhirResourceAppId = $value.Trim('"')
        }
    }
}

if (-not $FhirResourceAppId) {
    Write-Error "FhirResourceAppId is STILL not set. Exiting."
    exit
}

$graphEndpoint = "https://graph.microsoft.com/v1.0"
$appObjectId = (az ad app show --id $FhirResourceAppId --query "id" --output tsv)
$extensionUrl = "$graphEndpoint/applications/$appObjectId/extensionProperties"
$token = $(az account get-access-token --resource-type ms-graph --query accessToken --output tsv)

$body = "{
    `"name`": `"fhirUser`",
    `"dataType`": `"String`",
    `"targetObjects`": [`"User`"]
}"

Invoke-RestMethod -Uri $extensionUrl -Headers @{Authorization = "Bearer $token"} -Method Post -Body $body -ContentType application/json

Write-Host "Done."