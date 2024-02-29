<#
    Configure FHIR resource application registration manifest
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

$AppRoles = "$SCRIPT_PATH/manifest-json-contents/app-roles.json"
$OAuth2Permissions = "$SCRIPT_PATH/manifest-json-contents/oauth2-permissions.json"
 
    $APP_NAME=$(az ad app show --id $FhirResourceAppId --query 'displayName' --output tsv)

$DOMAIN_INFO=$(az rest --method get --url 'https://graph.microsoft.com/v1.0/domains?$select=id')
$DOMAIN_JSON = $DOMAIN_INFO | ConvertFrom-Json
$PRIMARY_DOMAIN = $DOMAIN_JSON.value[0].id

azd env set FhirAudience "https://$APP_NAME.$PRIMARY_DOMAIN"

az ad app update --id $FhirResourceAppId --identifier-uris "https://$APP_NAME.$PRIMARY_DOMAIN" --set appRoles=@$AppRoles api=@$OAuth2Permissions

Write-Host "Done."