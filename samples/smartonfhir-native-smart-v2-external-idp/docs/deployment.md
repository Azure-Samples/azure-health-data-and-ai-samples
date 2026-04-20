# Deployment Guide (External IDP + Minimal Infra)

This guide is for the simplified deployment model in this repo:

- New resource group
- New Azure Health Data Services FHIR service
- Azure Function App (token + context-cache endpoints)
- Azure Cache for Redis
- Monitoring (App Insights + Log Analytics)


## 1. Prerequisites

- Azure subscription with permission to create resources.
- External IDP already configured for SMART on FHIR (for example Okta auth server).
  - This deployment does not provision the external IDP itself.
  - You must provide an existing authority URL and valid token configuration.
- Installed tools:
  - `az` (Azure CLI)
  - `azd` (Azure Developer CLI)
  - `.NET 8 SDK`
  - `PowerShell 7+` (recommended for scripts in this repo)

## 2. Required configuration values

Before deployment, collect:

- **AuthorityURL**
  - OIDC authority/issuer base URL of your external IDP.
  - Example (Okta): `https://<okta-domain>/oauth2/default`
- **FhirAudience** (optional, recommended if you already know target audience)
  - If omitted, infra defaults to the newly created FHIR URL.
- **UserIdClaimType** (optional)
  - Claim used by token augmentation logic to identify user.
  - Default is `sub`; for some Okta setups use `uid`.
- **ContextAppClientId** (optional)
  - Needed only if you want strict caller validation for context-cache requests.

## 3. Login and initialize environment

From repository root:

```powershell
az login
azd auth login
azd env new <env-name>
```

Use lowercase alphanumeric env name (short name recommended).

## 4. Set azd environment values

Set the required environment values:

```powershell
azd env set AuthorityURL "https://<your-idp-authority>"
```

Optional values:

```powershell
azd env set FhirAudience "https://<expected-fhir-audience>"
azd env set UserIdClaimType "sub"
azd env set ContextAppClientId ""
```

Also set deployment location if needed:

```powershell
azd env set AZURE_LOCATION "eastus2"
```

## 5. Deploy infrastructure and function app

Run:

```powershell
azd up
```

`azd up` provisions infra from `infra/main.bicep` and deploys the Function service defined in `azure.yaml`.

## 6. Post-deployment verification

After deployment:

1. Check outputs from `azd`:
   - `AZURE_RESOURCE_GROUP`
   - `FhirUrl`
   - `FhirAudience`
   - `FunctionBaseUrl`
2. Confirm Function App settings in Azure Portal include:
   - `AZURE_FhirServerUrl`
   - `AZURE_FhirAudience`
   - `AZURE_Authority_URL`
   - `AZURE_CacheConnectionString`
3. Verify endpoints:
   - `POST <FunctionBaseUrl>/token`
   - `POST <FunctionBaseUrl>/context-cache`

## 7. Configure FHIR authentication for external IDP

The template sets FHIR authentication configuration with:

- `authority` = Azure Entra authority for the deployment tenant (`https://login.microsoftonline.com/<tenant-id>`)
- `audience` = resolved FHIR audience
- SMART identity provider authority = `AuthorityURL`

Validate in Azure Portal:

1. Open FHIR service.
2. Go to Authentication settings.
3. Confirm authority/audience values align with your IDP token configuration.

## 8. Redis connectivity behavior

No manual Redis connection string entry is required.

Deployment flow:

1. Redis is created.
2. Template reads Redis key and host.
3. Template composes connection string.
4. Template sets Function app setting `AZURE_CacheConnectionString`.

## 9. Troubleshooting

- If token forwarding fails, verify:
  - `AuthorityURL` is correct.
  - FHIR `/.well-known/smart-configuration` is reachable.
  - External IDP token endpoint is available.
- If launch context is missing:
  - Verify Redis app setting exists.
  - Verify `POST /api/context-cache` is called before token exchange in EHR launch flow.
- If claims are missing in token response:
  - Ensure IDP access token includes expected claims (`patient`, `fhirUser`, or mapping used by your flow).

For general issues, see `docs/troubleshooting.md`.
