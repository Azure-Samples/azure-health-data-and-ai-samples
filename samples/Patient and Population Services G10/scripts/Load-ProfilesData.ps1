<#
    Loads sample data and US Core profiles into a FHIR server.

    Uses the Azure CLI, NPM, .NET 6+ SDK, and the FHIR Loader CLI tool.
#>
param (
    # height of largest column without top bar
    [Parameter(Mandatory=$false)]
    [string]$FHIR_URL,
    
    # name of the output image
    [Parameter(Mandatory=$false)]
    [string]$FHIR_AUDIENCE
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName
$ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
Write-Host "Using Azure Account logged in with the Azure CLI: $($ACCOUNT.name) - $($ACCOUNT.id)"


if ([string]::IsNullOrWhiteSpace($FHIR_URL) -or [string]::IsNullOrWhiteSpace($FHIR_URL)) {

    Write-Host "FHIR_URL or FHIR_AUDIENCE is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) || $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FHIR_URL) -and $name -eq "FhirServerUrl") {
            $FHIR_URL = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($FHIR_URL) -and $name -eq "Audience") {
            $FHIR_AUDIENCE = $value.Trim('"')
        }
    }
}

Write-Host "Writing US Core v3.1.1 profiles and test data to FHIR_URL: $FHIR_URL with FHIR_AUDIENCE: $FHIR_AUDIENCE."

cd $HOME/Downloads
git clone https://github.com/microsoft/fhir-loader.git
cd $HOME/Downloads/fhir-loader

git checkout fhir-loader-cli
git pull

cd $HOME/Downloads/fhir-loader/src/FhirLoader.Tool/
dotnet pack

# Uninstall if already installed
dotnet tool uninstall FhirLoader.Tool --global
dotnet tool install --global --add-source ./nupkg FhirLoader.Tool

cd $HOME/Downloads/fhir-loader

mkdir sample-data

# Download sample data
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/microsoft/fhir-server/main/docs/rest/Inferno/V3.1.1_USCoreCompliantResources.json" -OutFile ./sample-data/V3.1.1_USCoreCompliantResources.json

# Load sample data
microsoft-fhir-loader --folder $HOME/Downloads/fhir-loader/sample-data --fhir $FHIR_URL --audience $FHIR_AUDIENCE --debug

# Download US Core
cd $HOME/Downloads
npm --registry https://packages.simplifier.net install hl7.fhir.us.core@3.1.1

# Load us core
microsoft-fhir-loader --package $HOME/Downloads/node_modules/hl7.fhir.us.core/ --fhir $FHIR_URL --audience $FHIR_AUDIENCE --debug