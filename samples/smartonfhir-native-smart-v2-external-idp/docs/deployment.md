> [!TIP]
> *If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).*

> **Note** – Throughout this document, "External IDP" refers to a non-Microsoft identity provider (for example, Okta) that you have already configured to issue OIDC-compliant tokens.

# Sample Deployment: SMART on FHIR (External IDP)

This document guides you through the steps needed to deploy this sample. The deployment provisions Azure infrastructure, deploys custom Function App code, and configures the FHIR service to trust your external Identity Provider.

*Note:* This sample deployment is streamlined for external IDP scenarios. On average it will take approximately **15–20 minutes** to deploy end to end.

---

## 1. Prerequisites

Make sure you have the prerequisites listed below before starting deployment.

### Installation

- [Git](https://git-scm.com/) to access the files in this repository.
- [Azure CLI Version 2.51.0 or greater](https://learn.microsoft.com/cli/azure/install-azure-cli) to run scripts that interact with Azure.
- [Azure Developer CLI Version 1.9.0 or greater](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd?tabs=baremetal%2Cwindows) to deploy the infrastructure and code for this sample.
- [Visual Studio](https://visualstudio.microsoft.com/), [Visual Studio Code](https://code.visualstudio.com/), or another development environment (for changing configuration or debugging the sample code).
- [.NET SDK Version 8+](https://learn.microsoft.com/dotnet/core/sdk) installed (for building the sample).
- [PowerShell Version 7 or greater](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed for running scripts (works for Mac and Linux too!).

### Access

- Access to an Azure Subscription with **Owner** privileges.
- Permissions to create resources in the target subscription.

### External IDP Configuration

Your external Identity Provider must be pre-configured before running this deployment. The deployment **does not** provision the external IDP itself.

> **Using Okta?** Follow the complete [Okta Setup Guide](./okta-setup.md) to create your authorization server, test users, custom claims, client applications, and scopes before continuing with this deployment.

You will need the following from your external IDP:

| Value | Description | Example |
|---|---|---|
| **Authority URL** | OIDC authority / issuer base URL | `https://dev-12345678.okta.com/oauth2/default` |
| **Client ID** | Application / client ID registered in your IDP | `0oa1abc2defGHIjkl5d7` |
| **Audience** | Token audience expected by FHIR service (optional — defaults to FHIR URL if omitted) | `https://myhealth-fhirdata.fhir.azurehealthcareapis.com` |

### Test User Accounts

To effectively test the application, create test user accounts in your external IDP (see [Okta Setup Guide — Create Test Users](./okta-setup.md#3-create-test-users) for Okta-specific steps):

- **Patient test user** — will be mapped to a FHIR `Patient` resource.
- **Practitioner test user** — will be mapped to a FHIR `Practitioner` resource.

Ensure each test user has a `fhirUser` claim configured in the IDP. For example:
- Patient: `https://<fhir-url>/Patient/PatientA`
- Practitioner: `https://<fhir-url>/Practitioner/PractitionerC1`

---

## 2. Prepare and deploy environment

### 2.1. Clone the repository

Use the terminal or your git client to clone this repo. Open a terminal to the `samples/smartonfhir-native-smart-v2-external-idp` folder.

```bash
git clone https://github.com/Azure-Samples/azure-health-data-and-ai-samples.git
cd azure-health-data-and-ai-samples/samples/smartonfhir-native-smart-v2-external-idp
```

### 2.2. Login with the Azure CLI

Login to your Azure tenant where the resources will be deployed:

```powershell
az login --tenant <your-azure-tenant-id>
azd auth login --tenant-id <your-azure-tenant-id>
```

**Example:**
```powershell
az login --tenant 72f988bf-86f1-41af-91ab-2d7cd011db47
azd auth login --tenant-id 72f988bf-86f1-41af-91ab-2d7cd011db47
```

### 2.3. Create a new deployment environment

Run `azd env new` to create a new deployment environment, keeping the following in mind:

- Environment name must **not exceed 18 characters** in length.
- Deployment fails if environment name contains **uppercase letters**.
- Use **numbers and lowercase letters only**.
- Environment name will be the **prefix for all of your resources**.

```powershell
azd env new <env-name>
```

**Example:**
```powershell
azd env new smartfhirokta01
```

### 2.4. Collect required configuration values

Before setting environment values, gather the following information:

| Parameter | Required | Description | Default |
|---|---|---|---|
| `AuthorityURL` | **Yes** | OIDC authority / issuer base URL of your external IDP | — |
| `FhirAudience` | No | Audience for SMART scopes. If omitted, defaults to the newly created FHIR service URL | Auto-generated FHIR URL |
| `UserIdClaimType` | No | Claim name in the access token containing the user identifier | `sub` |
| `ContextAppClientId` | No | Client ID for strict caller validation on context-cache requests | — |

### 2.5. Set azd environment values

**Required — set the external IDP authority URL:**

```powershell
azd env set AuthorityURL "<your-idp-authority-url>"
```

**Example (Okta):**
```powershell
azd env set AuthorityURL "https://dev-12345678.okta.com/oauth2/default"
```
```powershell
# Change if your IDP uses a different claim for user identity 
azd env set UserIdClaimType "uid"
```

**Optional values:**

```powershell
# Set only if you have a specific audience requirement
azd env set FhirAudience "https://<workspace>-fhirdata.fhir.azurehealthcareapis.com"

# Set only if you need caller validation for EHR launch context-cache requests
azd env set ContextAppClientId "<your-context-app-client-id>"

```

### 2.6. Deploy infrastructure and Function App

Initiate the deployment by executing the `azd up` command. This handles both infrastructure provisioning and code deployment.

> *Note:* This command requires at least **PowerShell 7**. Running it in any earlier version may result in failure.

```powershell
azd up
```

When running `azd up`, you will be prompted to:
1. Select the **subscription** to deploy to.
2. Select the **location** (Azure region) for deployment.

**What gets deployed:**

| Resource | Name Pattern | Description |
|---|---|---|
| Resource Group | `{env-name}-rg` | Contains all deployed resources |
| Azure Health Data Services Workspace | `{env-name}health` | FHIR workspace |
| FHIR Service | `{env-name}health/fhirdata` | FHIR R4 service with SMART identity provider config |
| Azure Function App | `{env-name}-auth-func` | .NET 8 isolated — token proxy and context cache |
| Azure Cache for Redis | `{env-name}-cache` | Launch context store (Basic C0, TLS 1.2) |
| Storage Account | `{env-name}funcsa` | Function App runtime storage |
| App Service Plan | `{env-name}-appserv` | Consumption (Dynamic Y1) hosting plan |
| Application Insights | `{env-name}-appins` | Telemetry and monitoring |
| Log Analytics Workspace | `{env-name}-la` | Centralized logging (30-day retention) |

---

## 3. Post-deployment verification

After deployment completes, verify the following:

### 3.1. Check deployment outputs

The `azd up` command will output the following values. Record them for later use:

| Output | Description | Example |
|---|---|---|
| `AZURE_RESOURCE_GROUP` | Name of the deployed resource group | `smartfhirokta01-rg` |
| `FhirUrl` | FHIR service base URL | `https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com` |
| `FhirAudience` | Token audience for FHIR service | `https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com` |
| `FunctionBaseUrl` | Function App base URL | `https://smartfhirokta01-auth-func.azurewebsites.net/api` |

You can retrieve these outputs at any time by running:

```powershell
azd env get-values
```

### 3.2. Verify Function App settings in Azure Portal

Navigate to the Function App in the Azure Portal and confirm the following **Application Settings** are present:

| Setting | Expected Value |
|---|---|
| `AZURE_FhirServerUrl` | `https://{env-name}health-fhirdata.fhir.azurehealthcareapis.com` |
| `AZURE_FhirAudience` | FHIR audience URL (same as `FhirUrl` unless overridden) |
| `AZURE_Authority_URL` | Your external IDP authority URL |
| `AZURE_CacheConnectionString` | Auto-populated Redis connection string |
| `AZURE_UserIdClaimType` | `sub` (or your custom claim) |
| `AZURE_ContextAppClientId` | Your context app client ID (if set) |
| `AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING` | Auto-populated |


## 4. Configure FHIR authentication for external IDP

The Bicep template automatically configures the FHIR service with:

| Configuration | Value | Description |
|---|---|---|
| `authority` | `https://login.microsoftonline.com/<tenant-id>` | Azure Entra authority for the deployment tenant |
| `audience` | Resolved FHIR audience | Token audience for the FHIR service |
| `smartIdentityProviders[0].authority` | Your `AuthorityURL` | External IDP trusted for SMART token validation |

### Validate in Azure Portal

1. Open the **FHIR service** from the `{env-name}-rg` resource group.
2. Navigate to **Settings** → **Authentication**.
3. Confirm:
   - **Authority** is set to your Azure tenant's login URL.
   - **Audience** matches your FHIR service URL (or custom audience).
   - **Identity Provider 1** shows your external IDP authority URL.

> [!IMPORTANT]
> If the `smartIdentityProviders` configuration does not appear, verify that your `AuthorityURL` environment value was set correctly before running `azd up`. You can redeploy with `azd up` after correcting the value.

---

## 5. Add sample data and US Core resources

To successfully test this sample using Postman or REST clients, both the US Core FHIR package and applicable test data need to be loaded.

Ensure the user account you are using has the **FHIR Data Contributor** role assigned to the FHIR service.

### Run the data loading script

The script automatically reads `FhirUrl`, `FhirAudience`, and `TenantId` from your `azd` environment if not explicitly provided.

**Windows:**
```powershell
powershell ./scripts/Load-ProfilesData.ps1
```

**Mac/Linux:**
```bash
pwsh ./scripts/Load-ProfilesData.ps1
```

**With explicit parameters (if not using azd environment):**

```powershell
powershell ./scripts/Load-ProfilesData.ps1 `
  -FhirUrl "https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com" `
  -FhirAudience "https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com" `
  -TenantId "<your-azure-tenant-id>"
```

This script loads:
- **US Core v6.1.0 compliant sample resources** (Patient, Observation, Condition, etc.)
- **US Core CapabilityStatement** for the FHIR server

To learn more about the sample data, read [Sample Data](./sample-data.md).

---

## 6. Map test users to FHIR resources

> **Using Okta?** If you followed the [Okta Setup Guide](./okta-setup.md), you have already completed this step in [Section 4 — Add Custom Claims](./okta-setup.md#4-add-custom-claims-fhiruser) and [Section 5 — Map FHIR Users](./okta-setup.md#5-map-fhir-users-to-test-accounts). Verify the values below match what you configured.

### Add `fhirUser` claim to test users

To properly integrate with the sample data, each test user in your external IDP must have a `fhirUser` claim mapped to a FHIR resource:

| Test User | `fhirUser` Claim Value |
|---|---|
| Patient | `<FhirUrl>/Patient/PatientA` |
| Practitioner | `<FhirUrl>/Practitioner/PractitionerC1` |

**Example (for Okta):**
- In Okta Admin → **Directory** → **Profile Editor** → select your authorization server profile.
- Add a custom claim `fhirUser` with the appropriate FHIR resource URL.

**Example claim values:**
```
Patient:      https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com/Patient/PatientA
Practitioner: https://smartfhirokta01health-fhirdata.fhir.azurehealthcareapis.com/Practitioner/PractitionerC1
```

> [!NOTE]
> The exact steps to add custom claims vary by IDP. Refer to your IDP's documentation:
> - **Okta:** [Customize tokens with a Groups claim](https://developer.okta.com/docs/guides/customize-tokens-groups-claim/)
---

**[Back to Previous Page](../README.md)**
