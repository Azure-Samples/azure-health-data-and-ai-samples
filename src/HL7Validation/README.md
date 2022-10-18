# HL7 Validation Function
    This Azure Function is used to validate the HL7 Messages with the help of NAHPI Tool. We'll cover everything from deploying infrastructure, debugging locally and deploying to Azure.		

## Prerequisites
- An Azure account with an active subscription.
- [.NET 6.0](https://dotnet.microsoft.com/download)
- [Azure Command-Line Interface (CLI)](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://docs.microsoft.com/azure/developer/azure-developer-cli/get-started?tabs=bare-metal%2Cwindows&pivots=programming-language-csharp#prerequisites)
- Visual Studio or Visual Studio Code
  - For Visual Studio, you will need the Azure Functions Tools. To add Azure Function Tools, include the Azure development workload in your Visual Studio installation.
  - For Visual Studio Code, you will need to install the [Azure Function Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local?tabs=v4%2Cwindows%2Ccsharp%2Cportal%2Cbash#install-the-azure-functions-core-tools).

### Prerequisite check

- In a terminal or command window, run `dotnet --version` to check that the .NET SDK is version 6.0 or later.
- Run `az --version` and `azd version` to check that you have the appropriate Azure command-line tools installed.

## Setting up

This function will create the below resource. These will be used both for local development and the Azure deployment.
- Function App (and associated storage account).
- Log Analytics Workspace (Function App logs)
- Application Insights (for monitoring your validaton exception).

### Deploy

1. Create a new directory on your local machine and open that directory in a terminal or command prompt.
2. User need to be logged in to Azure using az login
3. Deploy the needed resources with the below `azd` command. This will pull the function code and deploy needed Azure resources.

    ```dotnetcli
    azd up --template Azure-Samples/azure-health-data-services-samples
    ```

4. This will take about 20 minutes to deploy the FHIR Service.
    a. `If you have run this sample in the past, using the same environment name and location will reuse your previous resources.`


## Testing locally

### Visual Studio Code

1. Open this folder in Visual Studio Code (`src/HL7Validation/HL7Validation`).
2. You may be asked to install recommended extensions to run this function. Click "Yes" to install the needed tools.
    1. Relaunch Visual Studio Code if this is your first time working with the Azure Function Tools.
3. Start the HL7Validation function app by going to "Run and Debug" and selecting the play button (or hit F5 on your keyboard).
4. You can now test your code locally! Set a breakpoint and go to `http://localhost:7071/ValidateHL7` in your browser or API testing tool.

### Visual Studio

1. Open the `HL7Validation.sln` project inside of Visual Studio.
2. You can now debug and test your code locally! Set a breakpoint and go to `http://localhost:7034/ValidateHL7` in your browser or API testing tool.

## Deploying to Azure

1. Once you are ready to deploy to Azure, we can use azd. Run `azd deploy` from your terminal or command prompt.
2. The command will output an endpoint for your function app. Copy this.
3. Test the endpoint by going to `<Endpoint>/ValidateHL7` in your browser or API testing tool.

## Usage details

- `Program.cs` outlines how we can register the dependencies to validate the HL7 messages.
    - Register the AppInsightConnectionstring in ConfigureServices to log the validation error.
- Please refer `ValidateHL7Message.cs` file to understand how to validate the HL7Messages using NHAPI Parse Method.
    - You can create custom validation as per your requirements. Please refer to `CustomValidation.cs` and `MessageTypeRule.cs` in the validation folder to understand how to create them.
    - Use StrictValidation or DefaultValidation if custom validations are not required.
    - To know more about NHAPI please refer this [link](https://github.com/nHapiNET/nHapi).
- The HL7Validate function end point method is listed below.   
   - POST: Accept the HL7 message as input and validate it. If it's a success, then return 200 as the status code with the message type as the content, otherwise return 500 with error details.
   



