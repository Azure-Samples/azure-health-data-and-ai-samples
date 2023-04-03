<#
    Creates a fhirUser directory extension in Azure AD. This is required for the FHIR Server to work with Azure AD authentication.
#>
param (
    [Parameter(Mandatory=$false)]
    [string]$APP_ID
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName
$ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
Write-Host "Using Azure Account logged in with the Azure CLI: $($ACCOUNT.name) - $($ACCOUNT.id)"


if ([string]::IsNullOrWhiteSpace($APP_ID)) {

    Write-Host "APP_ID is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) || $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FHIR_URL) -and $name -eq "FhirResourceAppId") {
            $APP_ID = $value.Trim('"')
        }
    }
}

$graphEndpoint = "https://graph.microsoft.com/v1.0"
$appObjectId = (az ad app show --id $APP_ID --query "id" --output tsv)
$extensionUrl = "$graphEndpoint/applications/$appObjectId/extensionProperties"

$body = @{
    name          = "fhirUser" 
    dataType      = "String"
    targetObjects = @("User")
} | ConvertTo-Json

Write-Host "Creating fhirUser directory extension for app $APP_ID"

az rest --method post --url $extensionUrl --body $body

Write-Host "Done."