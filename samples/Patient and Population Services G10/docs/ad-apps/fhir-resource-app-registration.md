# FHIR Resource App Registration

This application registration is used to customize the access token sent to the FHIR Service. The SMART on FHIR logic inside Azure Health Data Services relies on the `fhirUser` claim inside the access token to restrict user access to their own compartment (e.g. patient can access their own data but not others). Microsoft is unable to allow custom claims mapping on the first-party Healthcare APIs application as it creates a [security hole for malicious applications](https://learn.microsoft.com/azure/active-directory/develop/reference-app-manifest#acceptmappedclaims-attribute). We must then create a custom application registration to protect the FHIR Service and change the audience in the FHIR Service to validate tokens against the custom application.

## Deployment (manual)

### 1. Create the application

- Open Azure AD in the Azure Portal
- Note your `Primary Domain` in the Overview blade of Azure AD.
- Go to `App Registrations`
- Create a new application. The name should match that of your FHIR Service.
- Click `Register` (ignore redirect URI).

### 2. Set the application URL
- Go to `Expose an API` blade.
- Set the application URL to https://<app-registration-name>.<Azure AD Primary Domain>.
  - For example `https://my-app-1.mytenant.onmicrosoft.com`.
  - Save the `Application URL` for later.

### 3. Add all the applicable FHIR Scopes.
- Go to the Manifest blade. Copy the `oauth2Permissions` JSON element from [fhir-app-manifest.json](./fhir-app-manifest.json) to the `oauth2Permissions` JSON element in your application manifest.