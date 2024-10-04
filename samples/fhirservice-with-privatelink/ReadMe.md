# Deploying Azure FHIR Service with Private Endpoint
 Follow this guide to deploy the FHIR Server using Azure Private Link for secure, private access within your network

# Prerequisites needed
1. An Azure account
    - You must have an active Azure account. If you don't have one, you can sign up [here](https://azure.microsoft.com/en-us/free/).

2. Virtual Network (VNet) and Subnet
    - A virtual network and subnet are required for deploying the private endpoint. If you don’t have one, you can create it by following [this guide](https://learn.microsoft.com/en-us/azure/virtual-network/quick-create-portal).

# 1. Overview
By integrating Azure Private Link, you can secure the FHIR service within a virtual network (VNet) and restrict access to authorized resources within your private network. This setup ensures that sensitive healthcare data remains private and inaccessible over the public internet.

## Deployed Components
When you deploy the ARM template, the following components will be created:

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

### Option B: Deploy manually through an ARM template
You can manually deploy the Azure FHIR Service with a private endpoint by using the ARM template.
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
	- <*path-to-template*>: Provide the path to the ARM/Bicep template file i.e. main.json under samples/fhirservice-with-privatelink folder.
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
