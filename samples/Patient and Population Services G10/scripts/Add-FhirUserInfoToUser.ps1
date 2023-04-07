<#
    Populates a fhirUser Directory Extension for a particular user with a particular value.
#>
param (
    [Parameter(Mandatory=$true)]
    [string]$UserObjectId,

    [Parameter(Mandatory=$true)]
    [string]$FhirUserValue,

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
        if ([string]::IsNullOrWhiteSpace($name) || $name.Contains('#')) {
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

if (-not $UserObjectId) {
    Write-Error "UserObjectId is not set. Exiting."
    exit
}

if (-not $FhirUserValue) {
    Write-Error "FhirUserValue is not set. Exiting."
    exit
}

$graphEndpoint = "https://graph.microsoft.com/beta"
$userUrl = "$graphEndpoint/users/$UserObjectId"
$appIdFormatted = $FhirResourceAppId.Replace("-", "")
$token = $(az account get-access-token --resource-type ms-graph --query accessToken --output tsv)

$body = "{
    `"extension_$($appIdFormatted)_fhirUser`": `"$FhirUserValue`"
}"

Invoke-RestMethod -Uri $userUrl -Headers @{Authorization = "Bearer $token"} -Method Patch -Body $body -ContentType application/json

Write-Host "Done."