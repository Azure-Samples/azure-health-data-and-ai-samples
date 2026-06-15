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

## Common prerequisites

Both paths need:

- An Azure subscription with permission to create resources in one of the supported regions (`eastus2`, `westus2`, `centralus`).
- [Git](https://git-scm.com/), [Azure CLI 2.51+](https://learn.microsoft.com/cli/azure/install-azure-cli), [Azure Developer CLI 1.9+](https://learn.microsoft.com/azure/developer/azure-developer-cli/install-azd), [.NET 8 SDK](https://learn.microsoft.com/dotnet/core/sdk), and [PowerShell 7+](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) installed locally.
- Two test user accounts in the chosen IdP — one *Patient* persona and one *Practitioner* persona.

IdP-specific prerequisites are detailed in each deployment guide.

## What gets deployed

| Component | EntraId | ExternalIdp |
| --- | :---: | :---: |
| Azure Health Data Services FHIR Service | ✓ | ✓ |
| Azure Function App (SMART gateway) | ✓ | ✓ |
| App Service Plan (Linux, dotnet-isolated 8) | ✓ | ✓ |
| Storage Account | ✓ | ✓ |
| Application Insights + Log Analytics | ✓ | ✓ |
| Backend Services Key Vault | ✓ | — |
| Azure Cache for Redis (optional) | opt | opt |

The Bicep entry point is `infra/main.bicep`; the `azd` service definition is in `azure.yaml`.

## Next steps

1. Choose your IdP path above.
2. Complete the deployment guide.
3. Load sample data and map test users (covered in each guide and in [Sample Data](./sample-data.md)).
4. Validate the deployment using the Postman or REST collections under `docs/postman/` and `docs/rest/`.

[Back to README](../README.md)
