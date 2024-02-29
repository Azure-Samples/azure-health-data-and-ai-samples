# Authorize User Input App

This small application is required to enable session based permission scoping for SMART on FHIR standalone application flows. It is responsible for showing the end user their currently approved scopes, a choice of enabling some/all of the scopes the application is requesting, and then redirecting users to the Microsoft Entra Id consent experience.

It works in concert with the `AppConsentInfo` API in the Azure Auth Custom Operations running in Azure Functions. This app gets the current user's consent information from Microsoft Entra Id and clears any consent records (if needed) before the Microsoft Entra Id consent experience is shown to the user.

This application requires setup in Microsoft Entra Id to behave properly. Please see the deployment document for information on setting up this application.