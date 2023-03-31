## NOTE: It's best to login to Azure CLI first before running this script. Make sure to select your tenant and subscription.

## NOTE: You will need .NET 6 SDK installed on your system. You can download it from https://dotnet.microsoft.com/download/dotnet/6.0

## NOTE: You will need npm installed on your system. You can download it from https://nodejs.org/en/download/

## Base URL of your Azure FHIR Service
$FHIR_URL = ""

$AUDIENCE = ""

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
microsoft-fhir-loader --folder $HOME/Downloads/fhir-loader/sample-data --fhir $FHIR_URL --audience $AUDIENCE

# Download US Core
cd $HOME/Downloads
npm --registry https://packages.simplifier.net install hl7.fhir.us.core@3.1.1

# Load us core
microsoft-fhir-loader --package $HOME/Downloads/node_modules/hl7.fhir.us.core/ --fhir $FHIR_URL --audience $AUDIENCE