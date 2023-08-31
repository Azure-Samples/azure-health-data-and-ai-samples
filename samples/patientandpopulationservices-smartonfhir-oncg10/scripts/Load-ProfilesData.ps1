<#
    Loads sample data and US Core profiles into a FHIR server.

    Uses the Azure CLI, NPM, .NET 6+ SDK, and the FHIR Loader CLI tool.
#>
param (
    [Parameter(Mandatory=$false)]
    [string]$FhirUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$FhirAudience,

    [Parameter(Mandatory=$false)]
    [string]$TenantId
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName

if ([string]::IsNullOrWhiteSpace($FhirUrl) -or [string]::IsNullOrWhiteSpace($FhirAudience) -or  [string]::IsNullOrWhiteSpace($TenantId)) {

    Write-Host "Required parameters parameter blank, looking in azd enviornment configuration...."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = azd env get-values --cwd $SAMPLE_ROOT
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) +or $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FhirAudience) -and $name -eq "FhirAudience") {
            $FhirAudience = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($FhirUrl) -and $name -eq "FhirUrl") {
            $FhirUrl = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($TenantId) -and $name -eq "TenantId") {
            $TenantId = $value.Trim('"')
        }
    }
}

if (-not $FhirAudience) {
    Write-Error "FhirAudience is STILL not set. Exiting."
    exit
}

if (-not $FhirUrl) {
    Write-Error "FhirUrl is STILL not set. Exiting."
    exit
}

if (-not $TenantId) {
    Write-Error "TenantId is STILL not set. Exiting."
    exit
}

az login -t $TenantId

$access_token = az account get-access-token --scope "$FhirAudience/user_impersonation" --query 'accessToken' -o tsv

Write-Host "Using token $access_token"

$FilePath = "$SCRIPT_PATH/test-resources/V3.1.1_USCoreCompliantResources.json"
az rest --uri $FhirUrl --method POST --body "@$FilePath" --headers "Authorization=Bearer $access_token" "Content-Type=application/json"

$FilePath = "$SCRIPT_PATH/test-resources/CapabilityStatement-us-core-server.json"
az rest --uri "$FhirUrl/CapabilityStatement/us-core-server" --method PUT --body "@$FilePath" --headers "Authorization=Bearer $access_token" "Content-Type=application/json"
