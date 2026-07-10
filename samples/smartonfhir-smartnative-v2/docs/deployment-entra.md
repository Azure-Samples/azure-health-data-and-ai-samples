# Deploy with Microsoft Entra ID

This guide walks through deploying the SMART on FHIR v2 native IdP-agnostic sample using **Microsoft Entra ID** as the upstream authorization server. In this mode the gateway translates SMART scopes to Entra application scopes, injects SMART launch context into token responses, and provides SMART v2 Backend Services support backed by a Key Vault.

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).

The deployment provisions:

- A new resource group
- An Azure Health Data Services workspace and FHIR Service
- An Azure Function App (the SMART gateway) and its dependencies (App Service Plan, Storage, App Insights, Log Analytics)
- A Key Vault for SMART Backend Services client registrations
- *(Optional)* an Azure Cache for distributed EHR launch context

You will also create two **Microsoft Entra App Registrations** before running `azd up`:

- **FHIR Resource App** — represents the FHIR API and carries the custom `fhirUser` claim mapping.
- **Auth Context Frontend App** — used by EHR launch initiators to deliver launch context to the gateway.

---

## 1. Prerequisites

In addition to the [common prerequisites](./deployment.md#common-prerequisites), this path needs:

- An Azure subscription with **Owner** privileges and the ability to assign Microsoft Entra ID roles.
- **Microsoft Entra ID Global Administrator** privileges (or equivalent) to:
  - Create app registrations.
  - Create Microsoft Graph directory extensions.
  - Configure Enterprise Application claim mappings.
- Two test user accounts in your Entra tenant (Patient persona and Practitioner persona) with their **object IDs** noted.

---

## 2. Sign in and create the deployment environment

From the repository root:

```powershell
az login --tenant <tenant-id>
azd auth login --tenant-id <tenant-id>
azd env new <env-name>
```

Naming rules for the environment:

- Lowercase letters and numbers only.
- 18 characters or fewer.
- Used as the prefix for all resource names.

You can switch environments with `azd env select <env-name>` and inspect the active one with `azd env list`.

---

## 3. Create the FHIR Resource App Registration

Follow the steps in [FHIR Resource App Registration](./ad-apps/fhir-resource-app-registration.md). When you are done you will have:

- A FHIR Resource App registered in your Entra tenant.
- An Application ID URI (the FHIR audience).
- A Microsoft Graph **directory extension** for the `fhirUser` attribute.
- The `FhirResourceAppId` set on the `azd` environment.

---

## 4. Create the Auth Context Frontend App Registration

Follow the steps in [Auth Context Frontend App Registration](./ad-apps/auth-context-frontend-app-registration.md). When you are done you will have:

- A second app registration (an SPA) used by EHR launch initiators to deliver launch context to the gateway.

---

## 5. Set environment values

Set the required value:

```powershell
azd env set IdpType "EntraId"
```

Optional values:

```powershell
# Only if your Entra tenant differs from the subscription tenant.
azd env set TenantId "<your-entra-tenant-id>"

# Only if you want a distributed EHR launch context cache (e.g. Azure Managed Redis).
azd env set AZURE_CacheConnectionString "<redis-connection-string>"

```

---

## 6. Deploy infrastructure and the Function App

```powershell
azd up
```

`azd up` will prompt for subscription, `AZURE_LOCATION`, and `IdpType` if they are not already set. The deployment provisions the resource group, FHIR Service, Function App, Key Vault, and supporting resources, then deploys the gateway code from `src/SMARTCustomOperations.AzureAuth/`.

When deployment completes, `azd` writes outputs to `.azure/<env-name>/.env`, including:

- `AZURE_RESOURCE_GROUP`
- `FhirUrl`
- `FhirAudience`
- `FhirResourceAppId`
- `TenantId`
- `FunctionBaseUrl`
- `FunctionAppManagedIdentityPrincipalId`
- `BackendServiceKeyVaultName` and `BackendServiceKeyVaultUri`

For subsequent code-only redeploys (no infrastructure changes), use:

```powershell
azd deploy auth
```


## 7. Add sample data and US Core resources

See [Sample Data](./sample-data.md) for the full procedure. In short, on Windows:

```powershell
powershell ./scripts/Load-ProfilesData.ps1
```

The user account running the script needs the **FHIR Data Contributor** role on the FHIR Service. The `azd up` deployment automatically grants this role to the deployer (via the `principalId` parameter), so you can run the script as the same user that ran `azd up`.

---

## 8. Map test users to the sample data

Each test user in Entra ID must have its `fhirUser` directory extension set to the matching sample data resource. Run the helper script for each test user:

```powershell
# Patient persona
powershell ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<FhirResourceAppId>" -UserObjectId "<patient-test-user-object-id>" -FhirUserValue "Patient/PatientA"

# Practitioner persona
powershell ./scripts/Add-FhirUserInfoToUser.ps1 -ApplicationId "<FhirResourceAppId>" -UserObjectId "<practitioner-test-user-object-id>" -FhirUserValue "Practitioner/PractitionerC1"
```

Then assign the **FHIR SMART User** role to each test user on the FHIR Service:

1. Open the FHIR Service in the Azure Portal.
2. Go to **Access control (IAM) → Add → Add role assignment**.
3. Select role **FHIR SMART User**, assign to the test users, and save.

This role is required for the SMART scope evaluation to apply on the FHIR Service.

---

## 9. Validate the deployment

Run a few quick checks to confirm the gateway and the FHIR Service are wired up correctly:

- `GET <FunctionBaseUrl>/.well-known/smart-configuration` returns the gateway-published version of the same document.

### End-to-end test with the SMART client sample app

Before running the SMART client sample app, register up to four client applications in Entra ID — one for each launch flow you want to demo:

> See [SMART Client App Registrations (Microsoft Entra ID)](./ad-apps/smart-client-app-registrations.md) for step-by-step instructions covering all four launches:
>
> 1. Standalone Patient Launch — Confidential client (Web)
> 2. Standalone Patient Launch — Public client (SPA)
> 3. EHR Practitioner Launch — Confidential client (Web)
> 4. Backend Service Client (Key Vault-backed `private_key_jwt`)

Then point the SMART client sample app at this deployment to exercise all four SMART v2 launch flows — **EHR launch**, **Standalone launch**, **Backend Services**, and **Refresh**:

> **SMART Client Sample App**: [SMART Client Application](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/personal/gkuber/smartnative-smart-v2/samples/SMART-Client-Application)

That repository documents how to:

- Configure the sample app with the **Client ID** (and secret, where applicable) of each registration above.
- Configure the sample app to point at this deployment's `FunctionBaseUrl` and `FhirUrl`.
- Drive each of the four launch flows end to end and inspect the resulting access tokens and FHIR responses.

Once the four flows succeed against your deployment, the Entra path is fully validated.

[Back to Deployment Overview](./deployment.md) · [Back to README](../README.md)
