# Deployment Overview

This sample currently supports **Microsoft Entra ID** as the upstream identity provider. Follow the linked deployment guide end to end.

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](./troubleshooting.md).

## Identity Provider

| Authorization Server | Follow this guide |
| --- | --- |
| **Microsoft Entra ID** (workforce tenant) | [Deploy with Microsoft Entra ID](./deployment-entra.md) |

<!-- Additional identity providers: add a row to the table above once they are supported. -->

The Entra deployment:

- Sets `IdpType` to `EntraId`.
- Creates a Key Vault for SMART **Backend Services** client registrations.
- Requires manual creation of two **Microsoft Entra App Registrations** (FHIR Resource API, Auth Context Frontend) before `azd up`.

For a deeper view of what runs, see the [Technical Guide](./technical-guide.md).

### Supported capability matrix

| Capability | Microsoft Entra ID | Deployment requirement |
| --- | :---: | --- |
| Standalone public/confidential launch | ✓ | Register the SMART client in Microsoft Entra ID. |
| EHR launch context injection | ✓ | Configure a launch initiator and `/api/context-cache`; use a Redis-compatible cache for multi-instance reliability. |
| Built-in EHR launch initiator page | ✓ | Create the Auth Context Frontend App registration and add the gateway redirect URI. |
| Built-in consent picker | ✓ | Create the Auth Context Frontend App and grant the Function managed identity its required Microsoft Graph application permissions after deployment. |
| Refresh token exchange | ✓ | Request `offline_access` and configure it on the client registration. |
| Backend Services | Gateway validates `private_key_jwt` and exchanges with Entra | Uses the deployed Key Vault for client registrations. |
| Token revocation enforcement | ✓ | Clients must send FHIR traffic through the gateway proxy; configure distributed cache for multi-instance consistency. |
| FHIR Bulk Data group export proxy | ✓ | Export storage is deployed automatically. Reused FHIR services require manual export configuration and RBAC. |

<!-- Additional identity providers: add a column to the table above once they are supported. -->

The deployment always creates the export storage account. It does **not** create a Redis resource; `AZURE_CacheConnectionString` must reference a separately provisioned Redis-compatible service.

## Common prerequisites

The deployment needs:

- An Azure subscription with permission to create resources in one of the supported regions (`eastus2`, `westus2`, `centralus`).
- [Git](https://git-scm.com/), [Azure CLI 2.51+](https://learn.microsoft.com/cli/azure/install-azure-cli), [Azure Developer CLI 1.9+](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd), [.NET 8 SDK](https://learn.microsoft.com/dotnet/core/sdk), and [PowerShell 7+](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed locally.
- Two test user accounts in Microsoft Entra ID — one *Patient* persona and one *Practitioner* persona.

Entra-specific prerequisites are detailed in the [deployment guide](./deployment-entra.md#1-prerequisites).

## Reusing an existing FHIR service (optional)

The deployment can be pointed at an **existing** Azure Health Data Services FHIR service instead of creating a new workspace + FHIR service. Set one `azd` environment variable before `azd up`:

```powershell
azd env set ExistingFhirServiceId "/subscriptions/<sub>/resourceGroups/<fhir-rg>/providers/Microsoft.HealthcareApis/workspaces/<ws>/fhirservices/<svc>"
```

Reuse mode is intentionally **strictly non-destructive** — the deployment reads the existing FHIR service but never writes to it. That means the caller is responsible for a few one-time configuration steps on the reused FHIR (audience/authority, and a FHIR Data Contributor role for the deployer if they want to load sample data). Details are called out in the [reuse-mode section](./deployment-entra.md#reuse-mode-optional) of the deployment guide.

Assumptions:

- The existing FHIR service is in the **same Azure subscription** as the deployment.
- The AHDS resource type is `Microsoft.HealthcareApis/workspaces/fhirservices` (the legacy `Microsoft.HealthcareApis/services` / API for FHIR is not supported by reuse mode).
- The sample components (Function App, Key Vault, monitoring) still deploy into a fresh resource group `<env-name>-rg`. Only the FHIR service lives in its own RG.

Leave `ExistingFhirServiceId` unset (or empty) to keep the default create-new behavior.

## Next steps

1. Complete the [Microsoft Entra ID deployment guide](./deployment-entra.md).
2. Load sample data and map test users (covered in the guide and in [Sample Data](./sample-data.md)).
3. Run the validation commands in the deployment guide, then use the companion [SMART Client Application](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/SMART-Client-Application) for end-to-end launch, refresh, backend-services, and export tests.

[Back to README](../README.md)
