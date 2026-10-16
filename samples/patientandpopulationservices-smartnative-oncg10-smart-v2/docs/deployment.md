# Deployment Overview

This sample supports two upstream identity provider modes. Pick the path that matches your environment and follow the linked deployment guide end to end.

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).

## Choose your Identity Provider

| Authorization Server | Follow this guide |
| --- | --- |
| **Microsoft Entra ID** (workforce tenant) | [Deploy with Microsoft Entra ID](./deployment-entra.md) |
| **External IdP** such as Okta, Auth0, Ping, or any SMART-capable OpenID Provider | [Deploy with External IdP (Okta)](./deployment-okta.md) |

The two paths deploy the **same Azure components** with the same Bicep templates. The differences are:

- `IdpType` is set to `EntraId` or `ExternalIdp`.
- The Entra path additionally creates a Key Vault for SMART **Backend Services** client registrations.
- The Entra path requires manual creation of two **Microsoft Entra App Registrations** (FHIR Resource API, Auth Context Frontend) before `azd up`.
- The External path requires the IdP authority URL and post-deploy configuration of the FHIR Service's identity provider whitelist.

For a deeper view of what runs in each mode, see the [Technical Guide](./technical-guide.md).

### Supported capability matrix

| Capability | EntraId | ExternalIdp | Deployment requirement |
| --- | :---: | :---: | --- |
| Standalone public/confidential launch | ✓ | ✓ | Register the SMART client in the selected IdP and allow it on the FHIR service. |
| EHR launch context injection | ✓ | ✓ | Configure a launch initiator and `/api/context-cache`; use a Redis-compatible cache for multi-instance reliability. |
| Built-in EHR launch initiator page | ✓ | — | Create the Auth Context Frontend App registration and add the gateway redirect URI. |
| Built-in consent picker | ✓ | — | Create the Auth Context Frontend App and grant the Function managed identity its required Microsoft Graph application permissions after deployment. |
| Refresh token exchange | ✓ | ✓ | Request `offline_access` and configure it on the client registration. |
| Backend Services | Gateway validates `private_key_jwt` and exchanges with Entra | Client calls the external IdP directly | Entra uses the deployed Key Vault; external IdPs require native backend-services configuration. |
| Token revocation enforcement | ✓ | ✓ | Clients must send FHIR traffic through the gateway proxy; configure distributed cache for multi-instance consistency. |
| FHIR Bulk Data group export proxy | ✓ | — | Export storage is deployed automatically. Reused FHIR services require manual export configuration and RBAC. |

The deployment always creates the export storage account. It does **not** create a Redis resource; `AZURE_CacheConnectionString` must reference a separately provisioned Redis-compatible service.

## Common prerequisites

Both paths need:

- An Azure subscription with permission to create resources in one of the supported regions (`eastus2`, `westus2`, `centralus`).
- [Git](https://git-scm.com/), [Azure CLI 2.51+](https://learn.microsoft.com/cli/azure/install-azure-cli), [Azure Developer CLI 1.9+](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd), [.NET 8 SDK](https://learn.microsoft.com/dotnet/core/sdk), and [PowerShell 7+](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed locally.
- Two test user accounts in the chosen IdP — one *Patient* persona and one *Practitioner* persona.

IdP-specific prerequisites are detailed in each deployment guide.

## Reusing an existing FHIR service (optional)

Both deployment paths can be pointed at an **existing** Azure Health Data Services FHIR service instead of creating a new workspace + FHIR service. Set one `azd` environment variable before `azd up`:

```powershell
azd env set ExistingFhirServiceId "/subscriptions/<sub>/resourceGroups/<fhir-rg>/providers/Microsoft.HealthcareApis/workspaces/<ws>/fhirservices/<svc>"
```

Reuse mode is intentionally **strictly non-destructive** — the deployment reads the existing FHIR service but never writes to it. That means the caller is responsible for a few one-time configuration steps on the reused FHIR (audience/authority for Entra, `smartIdentityProviders` for External IdP, and a FHIR Data Contributor role for the deployer if they want to load sample data). Details are called out in the reuse-mode boxes inside each per-IdP guide.

Assumptions:

- The existing FHIR service is in the **same Azure subscription** as the deployment.
- The AHDS resource type is `Microsoft.HealthcareApis/workspaces/fhirservices` (the legacy `Microsoft.HealthcareApis/services` / API for FHIR is not supported by reuse mode).
- The sample components (Function App, Key Vault, monitoring) still deploy into a fresh resource group `<env-name>-rg`. Only the FHIR service lives in its own RG.

Leave `ExistingFhirServiceId` unset (or empty) to keep the default create-new behavior.

## Next steps

1. Choose your IdP path above.
2. Complete the deployment guide.
3. Load sample data and map test users (covered in each guide and in [Sample Data](./sample-data.md)).
4. Run the validation commands in the selected deployment guide, then use the companion [SMART Client Application](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/SMART-Client-Application) for end-to-end launch, refresh, backend-services, and export tests.

[Back to README](../README.md)
