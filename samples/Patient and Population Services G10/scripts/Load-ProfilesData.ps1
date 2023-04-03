<#
    Loads sample data and US Core profiles into a FHIR server.

    Uses the Azure CLI, NPM, .NET 6+ SDK, and the FHIR Loader CLI tool.
#>
param (
    [Parameter(Mandatory=$false)]
    [string]$FhirUrl,
    
    [Parameter(Mandatory=$false)]
    [string]$FhirAudience
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName
$ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
Write-Host "Using Azure Account logged in with the Azure CLI: $($ACCOUNT.name) - $($ACCOUNT.id)"


if ([string]::IsNullOrWhiteSpace($FhirUrl) -or [string]::IsNullOrWhiteSpace($FhirUrl)) {

    Write-Host "FhirUrl or FhirAudience is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) || $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($FhirUrl) -and $name -eq "FhirUrl") {
            $FhirUrl = $value.Trim('"')
        }

        if ([string]::IsNullOrWhiteSpace($FhirUrl) -and $name -eq "FhirAudience") {
            $FhirAudience = $value.Trim('"')
        }
    }
}

Write-Host "Writing US Core v3.1.1 profiles and test data to FhirUrl: $FhirUrl with FhirAudience: $FhirAudience."

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
microsoft-fhir-loader --folder $HOME/Downloads/fhir-loader/sample-data --fhir $FhirUrl --audience $FhirAudience --debug

# Download US Core
cd $HOME/Downloads
npm --registry https://packages.simplifier.net install hl7.fhir.us.core@3.1.1

# Load us core
microsoft-fhir-loader --package $HOME/Downloads/node_modules/hl7.fhir.us.core/ --fhir $FhirUrl --audience $FhirAudience --debug