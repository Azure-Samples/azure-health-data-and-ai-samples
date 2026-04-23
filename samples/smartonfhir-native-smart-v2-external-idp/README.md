# Azure SMART on FHIR v2 – External Identity Provider Sample

> [!NOTE]
> This sample demonstrates **SMART on FHIR v2.0.0 with an external Identity Provider** (e.g., Okta). For other SMART on FHIR samples, see:
> - [SMART on FHIR v2.0.0 sample (Microsoft Entra ID / B2C / External ID)](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/smartonfhir-smart-v2)
> - [ONC (g)(10) SMART on FHIR v2.0.0 sample](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/patientandpopulationservices-smartonfhir-oncg10-smart-v2)
> - [SMART on FHIR v1.0.0 sample](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/tree/main/samples/smartonfhir-smart-v1)

This sample extends [Azure Health Data Services FHIR service](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview) with first-party Azure products to enable the [SMART on FHIR v2.0.0 Implementation Guide](https://hl7.org/fhir/smart-app-launch/STU2.2/) using an **external Identity Provider** such as [Okta](https://developer.okta.com/). It also includes:

- [Health Level 7 (HL7®) Version 4.0.1 Fast Healthcare Interoperability Resources Specification (FHIR®)](http://hl7.org/fhir/directory.html)
- [United States Core Data for Interoperability (USCDI)](https://www.healthit.gov/isa/us-core-data-interoperability-uscdi)
- [FHIR® US Core Implementation Guide STU V6.1.0](https://hl7.org/fhir/us/core/STU6.1/)
- [HL7® SMART Application Launch Framework Implementation Guide Release 2.2.0](https://hl7.org/fhir/smart-app-launch/index.html)
- [OpenID Connect Core 1.0 incorporating errata set 1](https://openid.net/specs/openid-connect-core-1_0.html)

---

## Prerequisites

- Azure Subscription with Owner privileges.
- External IDP already configured for SMART on FHIR (e.g., Okta authorization server). This deployment **does not** provision the external IDP itself.
- Installed tools:
  - [`az`](https://learn.microsoft.com/en-us/cli/azure/install-azure-cli) (Azure CLI)
  - [`azd`](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) (Azure Developer CLI)
  - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  - [PowerShell 7+](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell) (recommended for scripts)

---

## Architecture

The following Azure components are deployed with this sample:

| Component | Purpose |
|---|---|
| **Azure Health Data Services – FHIR Service** | Stores and retrieves FHIR resources. Serves SMART metadata at `/.well-known/smart-configuration`. Stores authentication configuration for the external IDP. |
| **Azure Function App (.NET 8 isolated)** | Hosts custom endpoints for token proxy (`/api/token`) and EHR launch context cache (`/api/context-cache`). Forwards token requests to the external IDP, augments responses with SMART launch context and claims. |
| **Azure Cache for Redis** | Persists launch context payloads used during EHR launch token augmentation. |
| **Azure Storage Account + App Service Plan** | Runtime dependencies for the Function App. |
| **Application Insights + Log Analytics** | Observability, diagnostics, and troubleshooting telemetry. |

---

## How It Works

### Standalone Launch

1. Client reads SMART metadata from FHIR `GET /.well-known/smart-configuration`.
2. Client performs authorization with the external IDP `/authorize`.
3. Client exchanges the authorization code via the Function App `POST /api/token`.
4. Function App forwards the token request to the external IDP and receives the token response.
5. Function App merges JWT claims and cached context into the response.
6. Client uses the access token to call FHIR APIs.

### EHR Launch

1. EHR posts launch payload to Function App `POST /api/context-cache`.
2. Function App stores the launch payload in Redis with a configurable expiry.
3. User launches the app; authorization flow proceeds as above.
4. During token exchange, the Function App retrieves cached launch context from Redis and injects it into the token response.

---

## Endpoint Reference

| Endpoint | Method | Description |
|---|---|---|
| `https://<workspace>-<fhir>.fhir.azurehealthcareapis.com` | Various | FHIR base endpoint — serves resources (`/Patient`, `/Observation`, `/metadata`) and SMART metadata (`/.well-known/smart-configuration`). |
| `<FunctionBaseUrl>/api/token` | `POST` | Receives OAuth token requests from SMART clients, forwards to external IDP, enriches response with context and claims. |
| `<FunctionBaseUrl>/api/context-cache` | `POST` | Accepts launch context payloads from EHR launch initiators, writes to Redis. |

---

## Getting Started

1. **Configure your external IDP** — If using Okta, follow the [Okta Setup Guide](./docs/okta-setup.md) to create your authorization server, test users, custom claims, and client applications.
2. **Deploy to Azure** — Follow the [Deployment Guide](./docs/deployment.md) to provision infrastructure and deploy the Function App.
3. **Register SMART client applications with the FHIR Service** — For each SMART client application that will request tokens from the external IDP, you must register its Client ID on the FHIR Service:

   1. Open the **FHIR Service** from the `{env-name}-rg` resource group in the [Azure Portal](https://portal.azure.com).
   2. Navigate to **Settings** → **Authentication**.
   3. Under **Identity Provider**, add the **Client ID** of the application registered in your external IDP.
   4. Click **Save**.

   > [!NOTE]
   > Authentication configuration changes can take up to **10 minutes** to propagate. Wait before testing token-based access.

   Repeat for each client application (Standalone Patient, EHR Practitioner, Backend Service) as per your use case that you configured in your external IDP.


---

## Testing the Sample


### SMART Client Application

A sample SMART client application is available at [`SMART-Client-Application`](../SMART-Client-Application/) (sibling folder). It simulates all three SMART launch flows:

- **Standalone Patient Launch** — launches outside an EHR session as a patient-facing app.
- **EHR Practitioner Launch** — launches within an EHR session with pre-existing patient context.
- **Backend Service** — server-to-server access using client credentials (no user interaction).

Refer to the README in that folder for setup and usage instructions.



### REST Client

HTTP request files for testing directly from VS Code (using the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension):

- [Backend Service Client](./docs/rest/backend_service_client.http)
- [Confidential Client](./docs/rest/confidential_client.http)
- [EHR Practitioner App](./docs/rest/ehr_practitioner_app.http)

---

## Configuration Reference

The following app settings are provisioned automatically by the infrastructure deployment:

| Setting | Description |
|---|---|
| `AZURE_FhirServerUrl` | FHIR service base URL |
| `AZURE_FhirAudience` | Token audience for SMART scopes |
| `AZURE_Authority_URL` | External IDP authority / issuer URL |
| `AZURE_CacheConnectionString` | Redis connection string (auto-provisioned) |
| `AZURE_UserIdClaimType` | Claim name for user identifier (default: `sub`) |
| `AZURE_ContextAppClientId` | Client ID for context-cache caller validation (optional) |
| `AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING` | Application Insights connection string |

---

## Sample Support

If you are having issues with the sample, please look at the [Troubleshooting Guide](./docs/troubleshooting.md).

If you have questions about this sample, please [submit a GitHub issue](https://github.com/Azure-Samples/azure-health-data-and-ai-samples/issues).

> This sample is custom code you must adapt to your own environment and is not supported outside of GitHub issues. This sample is targeted towards developers with intermediate Azure experience.

---

## Resources

- [Azure Health Data Services documentation](https://learn.microsoft.com/en-us/azure/healthcare-apis/)
- [SMART on FHIR Implementation Guide (STU 2.2)](https://hl7.org/fhir/smart-app-launch/STU2.2/)
- [FHIR® US Core Implementation Guide STU V6.1.0](https://hl7.org/fhir/us/core/STU6.1/)
- [Azure Developer CLI documentation](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/)
- [Azure Functions documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
