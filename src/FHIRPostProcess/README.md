# Azure Function Post Process for FHIR resources

This post process azure function will remove empty resources from FHIR Bundle. we have used `Hl7.Fhir.R4` .NET library for post processing operations, we can also change parsing validation conditions using `FhirJsonParser`, We'll cover everything from deploying infrastructure, debugging locally and deploying to Azure.

## Prerequisites

- An Azure account with an active subscription.
  - You need access to create resource groups, resources, and role assignments in Azure
- [.NET 6.0](https://dotnet.microsoft.com/download)
- [Azure Command-Line Interface (CLI)](https://docs.microsoft.com/cli/azure/install-azure-cli)
- [Azure Developer CLI](https://docs.microsoft.com/azure/developer/azure-developer-cli/get-started?tabs=bare-metal%2Cwindows&pivots=programming-language-csharp#prerequisites)
- Visual Studio or Visual Studio Code
  - For Visual Studio, you will need the Azure Functions Tools. To add Azure Function Tools, include the Azure development workload in your Visual Studio installation.
  - For Visual Studio Code, you will need to install the [Azure Function Core Tools](https://docs.microsoft.com/azure/azure-functions/functions-run-local?tabs=v4%2Cwindows%2Ccsharp%2Cportal%2Cbash#install-the-azure-functions-core-tools).

### Prerequisite check

- In a terminal or command window, run `dotnet --version` to check that the .NET SDK is version 6.0 or later.
- Run `az--version` and `azd version` to check that you have the appropriate Azure command-line tools installed.

## Setting up

This post process will create the below resources. These will be used both for local development and the Azure deployment.

- Function App (and associated storage account)
- Log Analytics Workspace (Function App logs)
- Application Insights (for monitoring your custom operation)

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

1. Open this folder in Visual Studio Code (`src/FHIRPostProcess/FHIRPostProcess`).
2. You may be asked to install recommended extensions for the repository. Click "Yes" to install the needed tools
    1. Relaunch Visual Studio Code if this is your first time working with the Azure Function Tools.
3. Start the FHIRPostProcess function app by going to "Run and Debug" and selecting the play button (or hit F5 on your keyboard).
4. You can now test your code locally! Set a breakpoint and go to `http://localhost:7071/FHIRPostProcessFunction` in your browser or API testing tool.

### Visual Studio

1. Open the `FHIRPostProcess.sln` project inside of Visual Studio.
2. Debug the FHIRPostProcess function inside of Visual Studio.
3. You can now test your code locally! Set a breakpoint and go to `http://localhost:7256/FHIRPostProcessFunction` in your browser or API testing tool.


## Deploying to Azure

1. Once you are ready to deploy to Azure, we can use azd. Run `azd deploy` from your terminal or command prompt.
2. The command will output ae endpoint for your function app. Copy this.
3. Test the endpoint by going to `<Endpoint>/FHIRPostProcessFunction` in your browser or API testing tool.


## Usage details

- `Program.cs` outlines how we can register the dependencies for Post processing of FHIR Bundle.
    - Register the AppInsightConnectionstring in ConfigureServices to log the post processing errors.
- Please refer `PostProcessor.cs` file where we have used `Hl7.Fhir.R4`.NET library to do post processing.
 - post processing involves two operations 
      - changing bundle type to `transaction`
      - removing empty resources from FHIR bundle.
- `Hl7.Fhir.R4` library also has inbuilt validation for invalid fields and its data For FHIR Bundle parsing which we can disable using `FhirJsonParser` settings.
- To know more about `Hl7.Fhir.R4` please refer this [link](https://github.com/FirelyTeam/firely-net-sdk/tree/develop-r4).
- The FHIR Post process function end point method is listed below   
  - PUT: Accepts the FHIR Bundle json as input and performs post processing in order to remove empty resources from Bundle and returns FHIR bundle json with bundle type as `transaction`.
