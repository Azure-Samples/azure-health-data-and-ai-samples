<#
    Populates a fhirUser Directory Extension for a particular user with a particular value.
#>
param (
    [Parameter(Mandatory=$true)]
    [string]$ApplicationId,

    [Parameter(Mandatory=$true)]
    [string]$UserObjectId,

    [Parameter(Mandatory=$true)]
    [string]$FhirUserValue
)

if ([string]::IsNullOrWhiteSpace($ApplicationId)) {

    Write-Host "ApplicationId parameter blank, looking in azd enviornment configuration...."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($ApplicationId) -and $name -eq "ApplicationId") {
            $FhirResourceAppId = $value.Trim('"')
        }
    }
}

if (-not $ApplicationId) {
    Write-Error "ApplicationId is STILL not set. Exiting."
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

$appIdFormatted = $ApplicationId.Replace("-", "")
$token = $(az account get-access-token --resource-type ms-graph --query accessToken --output tsv)

$body = "{
    `"extension_$($appIdFormatted)_fhirUser`": `"$FhirUserValue`"
}"

Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users/$UserObjectId" -Headers @{Authorization = "Bearer $token"} -Method Patch -Body $body -ContentType application/json

Write-Host "Done"