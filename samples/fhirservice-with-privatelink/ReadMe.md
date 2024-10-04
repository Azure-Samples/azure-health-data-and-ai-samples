# Deploying Azure FHIR Service with Private Endpoint
 Follow this guide to deploy the FHIR Server using Azure Private Link for secure, private access within your network

# Prerequisites needed
1. An Azure account
    - You must have an active Azure account. If you don't have one, you can sign up [here](https://azure.microsoft.com/en-us/free/).

2. Virtual Network (VNet) and Subnet
    - A virtual network and subnet are required for deploying the private endpoint. If you don’t have one, you can create it by following [this guide](https://learn.microsoft.com/en-us/azure/virtual-network/quick-create-portal).
3. Virtual Machine (VM)
	- A virtual machine is required to access the FHIR service through the private endpoint.
	- The VM must be in the same VNet where the FHIR service is deployed for secure access through the private endpoint.
	If you don’t have one, you can create it by following [this guide](https://learn.microsoft.com/en-us/azure/virtual-network/quick-create-portal#create-virtual-machines)

4. Postman
	- Once the Virtual Machine is set up, install [Postman](https://www.postman.com/downloads/) on it to access the FHIR service endpoints. Postman can be used to interact with the FHIR API.

# 1. Overview
By integrating Azure Private Link, you can secure the FHIR service within a virtual network (VNet) and restrict access to authorized resources within your private network. This setup ensures that sensitive healthcare data remains private and inaccessible over the public internet.

## Deployed Components
When you deploy the ARM/Bicep template, the following components will be created:

1. Azure FHIR Service
    - A managed service to store and manage healthcare data in FHIR format.

2. Private Endpoint
    - A private link that connects your FHIR service securely to your virtual network, making the service accessible only from within the VNet or peered VNets.

3. Private DNS Zone
    - A DNS zone that allows private domain resolution for the FHIR service within your VNet, ensuring that traffic remains within the private network.


# 2. Deploy Azure FHIR Service with Private Endpoint

### Option A: Deploy through Azure Portal
Use the Deploy to Azure button to deploy the Azure FHIR Service with a private endpoint through the Azure Portal.

[![Deploy to Azure](https://aka.ms/deploytoazurebutton)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fazure-health-data-and-ai-samples%2Frefs%2Fheads%2Fmain%2Fsamples%2Ffhirservice-with-privatelink%2Fmain.json/createUIDefinitionUri/https%3A%2F%2Fraw.githubusercontent.com%2FAzure-Samples%2Fazure-health-data-and-ai-samples%2Frefs%2Fheads%2Fmain%2Fsamples%2Ffhirservice-with-privatelink%2FuiDefForm.json)

Notes: <br>
- Choose a unique "Prefix for all resources" during deployment.

### Option B: Deploy manually through an ARM/Bicep template
You can manually deploy the Azure FHIR Service with a private endpoint by using the ARM/Bicep template.
<br />
<details>
<summary>Click to expand to see manual deployment instructions.</summary>

1. Clone this repo
	```azurecli
	git clone https://github.com/Azure-Samples/azure-health-data-and-ai-samples.git --depth 1
	```
2. Log in to Azure  
	Before you begin, ensure that you are logged in to your Azure account. If you are not already logged in, follow these steps:
	```
	az login
	```
3. Set the Azure Subscription  
	If you have multiple Azure subscriptions and need to specify which one to use for this deployment, use the az account set command:
	```
	az account set --subscription [Subscription Name or Subscription ID]
	```
	Replace [Subscription Name or Subscription ID] with the name or ID of the subscription you want to use for this deployment. You can find your subscription information by running az account list.

	**Note** : This step is particularly important if you have multiple subscriptions, as it ensures that the resources are deployed to the correct subscription.

4. If needed, create a resource group

	If you don't already have a resource group that you want to use, use the following command to create a resource group.  
	```
		az group create --name <resource_group_name> --location <location>
	```  
	Replace <*resource_group_name*> with your desired name and <*location*> with the Azure region where you want to create the resource group

5. Deploy the FHIR service
	Now, you can initiate the deployment using the Azure CLI
	```
	az deployment group create --resource-group<resource-group-name> --template-file <path-to-template> --parameters <path-to-parameter>
	```
	- <*resource-group-name*>: Replace this with the name of the resource group you want to use.
	- <*path-to-template*>: Provide the path to the ARM/Bicep template file i.e. main.json or main.bicep under samples/fhirservice-with-privatelink folder.
	- <*path-to-parameter*>: Specify the path to the parameters file i.e. main.parameters.json under samples/fhirservice-with-privatelink folder.
	<br><br>
	**NOTE** : Please update the **main.parameters.json** file with the configurations that you need.

	|Parameter   | Description   | Example Value |
	|---|---|---|
	| prefix | Unique prefix for naming resources.| "pvt"|
	| virtualNetworkName | Name of the virtual network to use.| "myVnet"
	|virtualNetworkResourceGroup| Resource group where the virtual network is located.| "myResourceGroup" |
	|subnetName|Name of the subnet to use.|"default"|
	

</details>
<br />

# 3. Access the FHIR Service Using Postman on Your Virtual Machine (VM)
Once the FHIR service is deployed and the private endpoint is created, you can use Postman installed on your Virtual Machine (VM) to interact with the FHIR API securely. Here’s how to set it up:

## Step 1: Connect to Your VM
- Open the Azure Portal and navigate to your Virtual Machine resource.
- Click on Connect to access your VM. You can use RDP, SSH, or Bastion depending on your configuration.
- Once connected, ensure that Postman is installed on your VM. 

## Step 2: Retrieve FHIR Service Endpoint
- In the Azure Portal, go to your deployed FHIR service resource.
- Under Overview, locate the FHIR service URL (e.g., https://[your-fhir-service-name].azurehealthcareapis.com).
Copy this URL. This will be the base URL for accessing the FHIR service through Postman.

## Step 3: Configure Postman
- Open Postman on your VM.
- Create a new GET request.
In the URL field, paste the FHIR service URL (e.g., https://[your-fhir-service-name].azurehealthcareapis.com/Patient).
- Add the required headers for authentication:
	- Authorization: You will need a valid Azure Active Directory (AAD) token to authenticate your request. Use the following steps to generate a token.

## Step 4: Get an Azure AD Token
- In Postman, go to Authorization and choose OAuth 2.0 and click on Get New Access Token.
Fill in the following fields:
1. Token Name: Choose a name for the token.
2. Grant Type: Authorization Code or Client Credentials (depending on your setup).
3. Callback URL: Can be any URL since it's not needed for client credentials flow.
4. Auth URL: https://login.microsoftonline.com/<tenant_id>/oauth2/v2.0/authorize
5. Access Token URL: https://login.microsoftonline.com/<tenant_id>/oauth2/v2.0/token
6. Client ID: Your Azure App registration’s client ID.
7. Client Secret: Your Azure App registration’s client secret.
8. Scope: https://<your-fhir-service-name>.azurehealthcareapis.com/.default<br>

Click Request Token and once you receive the token, click Use Token.

## Step 5: Send a Request to the FHIR Service
- In Postman, make sure the Authorization header includes your newly generated Bearer token.
- Add a GET request to retrieve FHIR data (e.g., GET https://[your-fhir-service-name].azurehealthcareapis.com/Patient).
- Click Send. If everything is set up correctly, you should receive a response from the FHIR service with a list of FHIR Patient resources.

## Step 6: Troubleshoot Connectivity Issues
- If you're unable to connect, ensure that:
The VM is in the same VNet where the private endpoint is deployed.
- Network security groups (NSGs) and firewalls allow traffic within the VNet.
- You are using the correct FHIR service URL and have a valid AAD token.


This setup allows you to securely query the FHIR service through the private endpoint using Postman, ensuring that all interactions are contained within your private network.