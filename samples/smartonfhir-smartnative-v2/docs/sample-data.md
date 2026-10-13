# Load Sample Data and US Core Resources

> [!TIP]
> If you encounter an issue, see the [Troubleshooting Guide](./troubleshooting.md#data-issues).

The repository includes [Load-ProfilesData.ps1](../scripts/Load-ProfilesData.ps1), which loads the bundled US Core capability statement and sample resources into the configured FHIR service.

## Prerequisites

- Azure CLI and PowerShell 7.
- An active `azd` environment for this sample, or explicit `FhirUrl`, `FhirAudience`, and `TenantId` parameters.
- The signed-in account must have **FHIR Data Contributor** on the FHIR service.

Create-new deployments grant the deployer this role automatically. Reuse-mode deployments do not modify role assignments on the existing FHIR service, so grant it manually before loading data.

## Run the loader

From the sample root:

```powershell
pwsh ./scripts/Load-ProfilesData.ps1
```

The script reads missing values from the active `azd` environment. You can also provide them explicitly:

```powershell
pwsh ./scripts/Load-ProfilesData.ps1 `
  -FhirUrl "https://<workspace>-<service>.fhir.azurehealthcareapis.com" `
  -FhirAudience "<fhir-audience>" `
  -TenantId "<tenant-id>"
```

The loader uses the bundled US Core 6.1 sample resources under `scripts/test-resources/`; do not edit the script to set deployment values.

## Verify

Confirm the capability statement and a sample resource are available:

```http
GET <FhirUrl>/CapabilityStatement/us-core-server
GET <FhirUrl>/Patient/PatientA
```

For information about storing implementation guides and profiles in the FHIR service, see [Store profiles in the FHIR service](https://learn.microsoft.com/azure/healthcare-apis/fhir/store-profiles-in-fhir).

[Back to Deployment Overview](./deployment.md)