## You can copy and paste this. Make sure to start in the `fhir-server` folder, the repo root.

$FHIR_URL = "https://workspace-fhirservice.fhir.azurehealthcareapis.com"
$REPO_DIR = $(Get-Location).Path

# Make sure you are logged in to the Azure CLI for your tenant

cd ../
git clone https://github.com/microsoft/fhir-loader.git
cd fhir-loader

git checkout fhir-loader-cli
git pull

cd .\src\FhirLoader.Tool\
dotnet pack

# Uninstall if already installed
dotnet tool uninstall FhirLoader.Tool --global
dotnet tool install --global --add-source .\nupkg\ FhirLoader.Tool

# Load sample data
microsoft-fhir-loader --folder $REPO_DIR/fhir-server/docs/rest/Inferno --fhir $FHIR_URL

# Download US Core
cd $HOME/Downloads
npm --registry https://packages.simplifier.net install hl7.fhir.us.core@3.1.1

# Load us core
microsoft-fhir-loader --package $HOME\Downloads\node_modules\hl7.fhir.us.core\ --fhir $FHIR_URL