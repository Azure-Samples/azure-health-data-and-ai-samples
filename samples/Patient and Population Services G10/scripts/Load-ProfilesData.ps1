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


#$ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
#Write-Host "Using Azure Account logged in with the Azure CLI: $($ACCOUNT.name) - $($ACCOUNT.id)"

if ([string]::IsNullOrWhiteSpace($FhirUrl) -or [string]::IsNullOrWhiteSpace($FhirUrl)) {

    Write-Host "FhirUrl or FhirAudience or TenantId is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FhirUrl) -and $name -eq "FhirUrl") {
            $FhirUrl = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($FhirAudience) -and $name -eq "FhirAudience") {
            $FhirAudience = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($TenantId) -and $name -eq "TenantId") {
            $TenantId = $value.Trim('"')
        }
    }
}

if (-not $FhirUrl) {
    Write-Error "FhirUrl is STILL not set. Exiting."
    exit
}

if (-not $FhirAudience) {
    Write-Error "FhirAudience is STILL not set. Exiting."
    exit
}

if (-not $TenantId) {
    Write-Error "TenantId is STILL not set. Exiting."
    exit
}

Write-Host "Writing sample test data to FhirUrl: $FhirUrl with FhirAudience: $FhirAudience to TenantId: $TenantId"

$curDir = Get-Location

try {

    $installedDotnetTools = (dotnet tool list --global)
    $install = $True

    if ($installedDotnetTools -contains "microsoft-fhir-loader")
    {
        if ($installedDotnetTools -contains "0.1.5        microsoft-fhir-loader")
        {
            Write-Information("microsoft-fhir-loader already installed, continuing...")
            $install = $false
        }
        else
        {
            Write-Information("microsoft-fhir-loader outdated, removing and reinstalling...")
            dotnet tool uninstall FhirLoader.Tool --global
        }
        
    }

    if ($install)
    {
        Write-Information("microsoft-fhir-loader not installed, installing now...")

        Set-Location $HOME/Downloads
        git clone https://github.com/microsoft/fhir-loader.git
        Set-Location $HOME/Downloads/fhir-loader

        git checkout fhir-loader-cli
        git pull

        Set-Location $HOME/Downloads/fhir-loader/src/FhirLoader.Tool/
        dotnet pack

        dotnet tool install --global --add-source ./nupkg FhirLoader.Tool
    }   
}
finally {
    Set-Location $curDir
}

# Load sample data
microsoft-fhir-loader --folder $SCRIPT_PATH/test-resources --fhir $FhirUrl --audience $FhirAudience --tenant-id $TenantId --debug
