# SMART on FHIR v2 (External IDP)

## Overview

This sample implements SMART on FHIR token handling with an external identity provider (for example Okta), Azure Health Data Services FHIR, and an Azure Function App.

The Function App is responsible for:

- forwarding token requests to the IDP token endpoint discovered from FHIR SMART metadata
- augmenting token responses with SMART launch context values
- storing and retrieving launch context in Redis for EHR launch flows

## Architecture

The solution deploys the following Azure components:

- **Azure Health Data Services FHIR Service**
  - Serves FHIR APIs and SMART metadata.
  - Stores authentication configuration for the external IDP.
- **Azure Function App (.NET 8 isolated)**
  - Hosts custom endpoints for token proxy and context cache operations.
- **Azure Cache for Redis**
  - Persists launch context payloads used during token augmentation.
- **Storage Account + App Service Plan**
  - Runtime dependencies for the Function App.
- **Application Insights + Log Analytics**
  - Observability and troubleshooting telemetry.

## Endpoint Behavior

- `FHIR base endpoint`
  - `https://<workspace-name>-<fhir-service-name>.fhir.azurehealthcareapis.com`
  - Serves FHIR resources directly (for example `/Patient`, `/Observation`, `/metadata`).
  - Also serves SMART metadata at:
    - `GET /.well-known/smart-configuration`

- `POST /api/token`
  - Receives OAuth token requests from SMART clients.
  - Forwards to the external IDP token endpoint.
  - Enriches token response with context and claims when available.

- `POST /api/context-cache`
  - Accepts launch context payloads from EHR launch initiators.
  - Writes launch context to Redis with expiry.

## Communication Flow

1. Client reads SMART metadata from FHIR `/.well-known/smart-configuration`.
2. Client performs authorization with external IDP `/authorize`.
3. Client exchanges code using Function `POST /api/token`.
4. Function forwards token request to IDP and receives token response.
5. Function merges JWT claims and Redis launch context into the response.
6. Client uses access token to call FHIR APIs.

EHR launch sequence:

1. EHR posts launch payload to `POST /api/context-cache`.
2. Function stores launch payload in Redis.
3. Token exchange retrieves that context and injects it into token response.

## Function App Configuration

These app settings are provisioned by infrastructure:

- `AZURE_FhirServerUrl`
- `AZURE_FhirAudience`
- `AZURE_Authority_URL`
- `AZURE_CacheConnectionString`
- `AZURE_UserIdClaimType`
- `AZURE_ContextAppClientId` (optional)
- `AZURE_APPLICATIONINSIGHTS_CONNECTION_STRING`

Key deployment outputs:

- `FhirUrl` - FHIR base endpoint
- `FunctionBaseUrl` - Function base endpoint

## Project Layout

- `src/SMARTCustomOperations.AzureAuth` - Function code and pipeline filters
- `infra/main.bicep` - Infra entrypoint
- `infra/core` - FHIR, Redis, monitoring, and function host modules
- `infra/app/authCustomOperation.bicep` - Function App resource and settings
- `docs/deployment.md` - End-to-end deployment and configuration steps
