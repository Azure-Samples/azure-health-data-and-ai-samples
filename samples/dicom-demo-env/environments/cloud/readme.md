# Deploy RSNA demo infrastructure
This demo script deploys an AHDS DICOM service and a connected MedDream ZFP viewer.

## Prerequisites
- You need to create an App Registration and provide a few key variables.
- 

## Steps
  - Azure Portal
    -  click [![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FStevenBorg%2Fahds_dicom_service_demos%2Fmain%2Fdemos%2Fdicom-service-with-meddream%2Fdeploy-dicom-with-meddream.json)
    - Fill in required information
      - Click *Review + create*
      - Click *Create*
      - ![Steps to deploy using the Portal](../../readme-images/steps-deploy-infra-using-portal.png "Steps to deploy using the Portal").
  - ACI command line with Bicep
    - Clone this repo
    - Open a command line that has Azure CLI support and navigate to this folder (`./ahds_dicom_service_demos/demos/rsna)
    - Type the following commands:
      - `az login`
      - `az account set -s "<desired Azure Subscription Name>"  `
      - `az group create --location eastus --name yourUniqueResourceGroupName `
      - `az deployment group create --template-file .\deploy-rsna-demo-on-prem.bicep --resource-group yourUniqueResourceGroupName`
    - Enter a complex password that will be used later to log into the Jump-VM virtual machine that will be created
    - Wait until you have a successful deployment