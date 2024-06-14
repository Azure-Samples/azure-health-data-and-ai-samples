## 4. Add sample data and US Core resources

The Inferno (g)(10) suite requires both the US Core profile and data to be loaded in order to pass the test. 

We have created a Powershell script that will load US Core artifacts and test data quickly for testing. Open [this script](../scripts/Load-ProfilesData.ps1), change the variables at the top, and execute.

### Loading the US Core resources

Information about loading profiles for the FHIR Service can be found at [this documentation page](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/store-profiles-in-fhir). In general, you will want to load all of the artifacts that are part of the US Core package.

### Loading Data

Passing Inferno (g)(10) may require loading of real data from your application. To quickly test this solution, you can load some sample data.

We have created a bundle containing all the needed resources to pass the Inferno test. This can be found [here](https://raw.githubusercontent.com/microsoft/fhir-server/main/docs/rest/Inferno/V3.1.1_USCoreCompliantResources.json). Postman or another REST client can be used to load this file.
