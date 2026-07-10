# FHIR Resource App Registration

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](../troubleshooting.md).

This application registration represents the **FHIR API** in Microsoft Entra ID. The SMART on FHIR logic in the FHIR Service relies on the `fhirUser` claim inside the access token to scope user access to their own compartment (e.g. a Patient can access only their own data).

Microsoft does not allow custom claim mappings on the first-party Healthcare APIs application because doing so would be a security risk for malicious applications (see [acceptMappedClaims attribute](https://learn.microsoft.com/azure/active-directory/develop/reference-app-manifest#acceptmappedclaims-attribute)). To work around that, the sample uses a custom application registration as the FHIR audience and applies the `fhirUser` claim mapping there.

This is required only when deploying with `IdpType=EntraId`. External IdPs handle SMART claim mapping inside their own authorization servers.

---

## Steps

### 1. Create the application registration

1. Sign in to the **Microsoft Azure Portal** in your tenant.
2. Navigate to **Microsoft Entra ID → App registrations → New registration**.
3. Set the following values:
   - **Name**: match your `azd` environment name (lowercase, no spaces) or use a custom name without whitespace.
   - **Supported account types**: *Accounts in this organizational directory only*.
   - **Redirect URI**: leave blank.
4. Click **Register**.
5. Record the **Application (client) ID** — this is `FhirResourceAppId`.

### 2. Tell `azd` about the application

From the repository root:

```powershell
azd env set FhirResourceAppId "<application-id-from-step-1>"
```

### 3. Configure the application manifest

Run the helper script to apply the required manifest changes. The script:

- Sets the `identifierUris` (Application ID URI) on the app registration to `https://<app-name>.<tenant-primary-domain>`.
- Adds the SMART app roles and OAuth2 permissions defined under `scripts/manifest-json-contents/`.
- Writes the resulting URI to the active `azd` environment as `FhirAudience` automatically.

Windows:

```powershell
powershell ./scripts/Configure-FhirResourceAppRegistration.ps1
```

Mac / Linux:

```powershell
pwsh ./scripts/Configure-FhirResourceAppRegistration.ps1
```

You can verify the values afterwards with:

```powershell
azd env get-values | findstr Fhir
```

### 4. Create the `fhirUser` directory extension

The `fhirUser` claim is stored against each user as a **Microsoft Graph directory extension** owned by the FHIR Resource App. Create that extension once per tenant:

Windows:

```powershell
powershell ./scripts/Create-FhirUserDirectoryExtension.ps1
```

Mac / Linux:

```powershell
pwsh ./scripts/Create-FhirUserDirectoryExtension.ps1
```

### 5. Map the directory extension into the access token

Follow [Set fhirUser Claim Mapping](./set-fhir-user-mapping.md) to map the directory extension into the `fhirUser` claim of the access token.

---

## Outcome

When you finish you will have:

- `FhirResourceAppId` set on the `azd` environment.
- `FhirAudience` set on the `azd` environment.
- A directory extension for `fhirUser` registered against the new application.
- The Enterprise Application configured to emit `fhirUser` in access tokens.

[Back to Microsoft Entra ID Deployment](../deployment-entra.md)
