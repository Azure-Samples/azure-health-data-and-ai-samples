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

    Write-Host "FhirResourceAppId is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) || $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FHIR_URL) -and $name -eq "FhirResourceAppId") {
            $FhirResourceAppId = $value.Trim('"')
        }
    }
}

$graphEndpoint = "https://graph.microsoft.com/v1.0"
$userUrl = "$graphEndpoint/users/$UserObjectId"
$appIdFormatted = $FhirResourceAppId.Replace("-", "")

$body = [PSCustomObject]@{}
$body | Add-Member -MemberType NoteProperty -Name "extension_$($appIdFormatted)_fhirUser" -Value $FhirUserValue
$bodyString =  $body | ConvertTo-Json
Write-Host "Updating user $UserObjectId with fhirUser info: $bodyString"

az rest --method patch --url $userUrl --body $bodyString

Write-Host "Done."