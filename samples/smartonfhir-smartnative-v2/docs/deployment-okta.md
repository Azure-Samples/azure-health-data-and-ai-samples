# Deploy with External IdP (Okta)

This guide walks through deploying the SMART on FHIR v2 native IdP-agnostic sample using an **external identity provider** as the upstream authorization server. Okta is used as the running example; the same steps apply to any SMART-capable OpenID Provider (Auth0, Ping, etc.) that can issue SMART scopes and `fhirUser` claims natively.

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).

The deployment provisions:

- A new resource group (`<env-name>-rg`)
- An Azure Health Data Services workspace and FHIR Service *(skipped when reusing an existing FHIR service — see [Reuse mode](#reuse-mode-optional) below)*
- An Azure Function App (the SMART gateway) and its dependencies (App Service Plan, Storage, App Insights, Log Analytics)
- *(Optional)* an Azure Cache for distributed EHR launch context

A Key Vault is **not** deployed in this mode. SMART Backend Services on an External IdP are handled directly by the IdP and do not transit the gateway.

---

## 1. Prerequisites

In addition to the [common prerequisites](./deployment.md#common-prerequisites), this path needs:

- An **Okta Authorization Server** (or equivalent SMART-capable OIDC provider) you control, with admin access to register applications, configure claim mappings, and manage user profile attributes.
- Two test user accounts in the IdP (Patient persona and Practitioner persona).

---

## 2. Configure the external IdP (Okta)

Before you deploy any Azure resources, configure the upstream authorization server.

Follow [Configure Okta as the SMART Authorization Server](./external-idp/okta-configuration.md). When you are done you will have:

- An **Authorization Server** with the SMART scopes enabled.
- An **`AuthorityURL`** (issuer URL) and **`FhirAudience`** value to set on the `azd` environment.
- Claim mappings on the access token for `fhirUser`, `patient`, `encounter`, `launch`, and `scope`.
- A `fhirUser` custom attribute on the user profile, mapped onto your test users.
- *(Optional)* SMART Backend Services application registrations.

---

## 3. Initialize the deployment environment

From the repository root:

```powershell
az login
azd auth login
azd env new <env-name>
```

Naming rules for the environment:

- Lowercase letters and numbers only.
- 18 characters or fewer.
- Used as the prefix for all resource names.

You can switch between environments later with `azd env select <env-name>` and inspect the active one with `azd env list`.

---

## 4. Set environment values

Set the required values:

```powershell
azd env set IdpType      "ExternalIdp"
azd env set AuthorityURL "<authority-url-from-step-2>"
```

Optional values:

```powershell
azd env set FhirAudience              "<audience-from-step-2>"
azd env set ContextAppClientId        "<context-app-client-id>"
azd env set AZURE_CacheConnectionString "<redis-connection-string>"
azd env set AZURE_LOCATION            "eastus2"

# Only if you want to reuse an existing AHDS FHIR service instead of creating a new one.
# See the "Reuse mode" section below for the manual configuration steps required afterwards.
azd env set ExistingFhirServiceId     "/subscriptions/<sub>/resourceGroups/<fhir-rg>/providers/Microsoft.HealthcareApis/workspaces/<ws>/fhirservices/<svc>"
```

- `FhirAudience` — leave blank to default to the FHIR Service URL after deployment.
- `ContextAppClientId` — set only if you front the gateway's `/context-cache` endpoint with an EHR launch initiator whose token audience needs to be enforced. Optional for most setups.
- `AZURE_CacheConnectionString` — leave blank to use in-memory caching inside the Function App (single-instance only).
- `AZURE_LOCATION` — use only `eastus2`, `westus2`, or `centralus`.

---

## 5. Deploy infrastructure and the Function App

```powershell
azd up
```

`azd up` will prompt for any values not already set (subscription, location, `IdpType`, `AuthorityURL`). The deployment provisions the resource group, FHIR Service, Function App, and supporting resources, then deploys the gateway code from `src/SMARTCustomOperations.AzureAuth/`.

When deployment completes, `azd` writes outputs to `.azure/<env-name>/.env`, including:

- `AZURE_RESOURCE_GROUP`
- `FhirUrl`
- `FhirAudience`
- `FhirServiceId` — full resource id of the FHIR service (new or reused)
- `FhirResourceGroup` — RG containing the FHIR service (equal to `AZURE_RESOURCE_GROUP` in create-new mode; the reused FHIR's RG in reuse mode)
- `FunctionBaseUrl`
- `FunctionAppManagedIdentityPrincipalId`

For subsequent code-only redeploys (no infrastructure changes), use:

```powershell
azd deploy auth
```

### Reuse mode (optional)

If `ExistingFhirServiceId` was set in step 4, `azd up` **does not** touch the existing FHIR service. Two things the create-new path does automatically must therefore be done by you, one time, against the reused FHIR:

1. **Add the SMART identity provider entry** on the reused FHIR service pointing at your Okta `AuthorityURL` with the correct `audience`. In create-new mode the Bicep template seeds this with a placeholder `applications[]` entry; in reuse mode nothing is written, so you must add the entry yourself.

   Portal: open the FHIR service → **Authentication** → under **SMART Identity Providers** add an entry with:
   - **Authority** = `AuthorityURL` from step 2
   - **Audience** = `FhirAudience` from step 2
   - **Applications** = the `client_id` of each SMART client that will call this FHIR service (with `Read` allowed data action)

   Save. See step 6 below for the full applications-whitelist details — it applies identically in reuse mode, you just start from an empty list instead of a placeholder.

2. **Grant yourself FHIR Data Contributor** on the reused FHIR service if you plan to run `Load-ProfilesData.ps1` or hit the data plane directly. Reuse mode intentionally skips this role assignment (see the note in step 7 below).

Everything else — the Function App, monitoring, and the app settings that point the gateway at `FhirUrl` / `FhirAudience` / `AuthorityURL` — is wired up automatically.

---

## 6. Register SMART client IDs on the FHIR Service

The Bicep template **automatically** creates the SMART identity-provider entry on the FHIR Service with `authority = <AuthorityURL>` and `audience = <FhirAudience>`. It also seeds the `applications[]` list with a single **placeholder** entry whose `clientId` is the literal string `ExternalAppClientId`.

You must replace that placeholder with the real `client_id` of every SMART application that will call this FHIR Service through your external IdP.

1. In the Azure Portal, open the deployed FHIR Service (resource group `<env-name>-rg`).
2. Go to **Settings → Authentication**.
3. Confirm the SMART identity provider entry shows your `AuthorityURL` and the FHIR audience.
4. Under **Applications** for that entry:
   - **Remove** the placeholder `ExternalAppClientId`.
   - **Add** the `client_id` of each confidential, public, or backend-service SMART client (with `Read` data action) that should be allowed to call the FHIR Service.
5. Save.

> The FHIR Service validates each access token's `appid` claim (which you configured in [Okta step D](./external-idp/okta-configuration.md#d-configure-claim-mappings-on-the-access-token)) against this list. Any SMART client whose `aud` matches the FHIR audience but whose `client_id` is not in `applications[]` will be rejected with HTTP 401.
>
> Authentication configuration changes can take up to 10 minutes to propagate.

---

## 7. Add sample data and US Core resources

See [Sample Data](./sample-data.md) for the full procedure. In short:

```powershell
pwsh ./scripts/Load-ProfilesData.ps1 -FhirAudience "<your FHIR audience>"
```

The user account running the script needs the **FHIR Data Contributor** role on the FHIR Service. The `azd up` deployment automatically grants this role to the deployer (via the `principalId` parameter) **in create-new mode only**. In [reuse mode](#reuse-mode-optional) the role is intentionally not granted — assign it manually on the reused FHIR before running the script, or skip data loading entirely if the reused FHIR already contains the data you need.

> Confirm the test users you mapped in [step 2](./external-idp/okta-configuration.md#f-map-the-test-users) point at resources that exist in this loaded sample data (e.g. `Patient/PatientA`, `Practitioner/PractitionerC1`).

---

## 8. Validate the deployment

Run a few quick checks to confirm the gateway and the FHIR Service are wired up correctly:

- `GET <FhirUrl>/.well-known/smart-configuration` returns SMART metadata whose `authorization_endpoint` and `token_endpoint` come from your IdP.
- `GET <FunctionBaseUrl>/.well-known/smart-configuration` returns the gateway-published version of the same document.

### End-to-end test with the SMART client sample app

To exercise all four SMART v2 launch flows against this deployment — **EHR launch**, **Standalone launch**, **Backend Services**, and **Refresh** — use the companion SMART client sample application:

> **SMART Client Sample App**: [SMART Client Application](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/SMART-Client-Application)

That repository documents how to:

- Register the client application(s) in your IdP and assign the SMART scopes you want to test.
- Configure the sample app to point at this deployment's `FunctionBaseUrl` and `FhirUrl`.
- Drive each of the four launch flows end to end and inspect the resulting access tokens and FHIR responses.
- Run a Backend Services `client_credentials` exchange directly against the IdP (no gateway involvement on the External IdP path).

Once the four flows succeed against your deployment, the External IdP path is fully validated.

[Back to Deployment Overview](./deployment.md) · [Back to README](../README.md)
