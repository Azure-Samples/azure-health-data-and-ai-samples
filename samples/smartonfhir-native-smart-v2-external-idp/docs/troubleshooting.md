# Troubleshooting

> [!TIP]
> Start with **Application Insights** for any HTTP errors — it provides the most detailed trace and request information.

---

## Application Insights

This sample deploys Application Insights connected to the Azure Function App. This is the best place to look if you are seeing any HTTP errors. It gives you insights into configuration, request/response data, and dependencies (Redis, external IDP, FHIR service).

**Where to look:**
1. Open Application Insights from the `{env-name}-rg` resource group.
2. Navigate to **Failures** blade to see failed requests.
3. Use **Transaction search** to trace individual requests end-to-end.
4. Check **Live Metrics** for real-time debugging during testing.

---

## Common Issues

### Token Forwarding Fails

If the Function App cannot forward token requests to the external IDP:

- Verify `AuthorityURL` is correct and resolves to a valid OIDC discovery endpoint:
  ```bash
  # Should return a valid OIDC discovery document
  curl https://dev-12345678.okta.com/oauth2/default/.well-known/openid-configuration
  ```
- Confirm FHIR `/.well-known/smart-configuration` is reachable:
  ```bash
  curl https://<env-name>health-fhirdata.fhir.azurehealthcareapis.com/.well-known/smart-configuration
  ```
- Confirm the external IDP token endpoint is available and not rate-limited.
- Check Application Insights → **Failures** → look for HTTP 400/401/500 responses from the `/api/token` endpoint.

### Launch Context Is Missing

If the EHR launch flow does not return launch context in the token response:

- Verify the `AZURE_CacheConnectionString` app setting exists in the Function App.
- Verify `POST /api/context-cache` is called **before** the token exchange in the EHR launch flow.
- Check Redis connectivity in Application Insights → **Failures** blade.
- Verify the Redis cache (`{env-name}-cache`) is running in the Azure Portal.

### Claims Are Missing in Token Response

If the token response does not contain expected claims (`patient`, `fhirUser`, `scope`):

- Decode the access token at [jwt.ms](https://jwt.ms/) to inspect the claims present.
- Ensure the IDP is configured to include `fhirUser`, `patient`, and `scope` claims in the access token.
- Verify the `AZURE_UserIdClaimType` setting matches the claim used by your IDP (default: `sub`).
- For Okta: check your Authorization Server → **Claims** tab to confirm custom claims are configured.

### Unauthorized Errors (401 / 403)

If you encounter `Unauthorized (401)` or `Forbidden (403)` errors:

- **User configuration:**
  - Test user is mapped with the appropriate `fhirUser` claim in the external IDP.
  - Required scopes were selected during authentication.
  - Decode the token at [jwt.ms](https://jwt.ms/) to identify `fhirUser` and `scope` claim values.

- **FHIR service configuration:**
  - Open FHIR service → **Settings** → **Authentication**.
  - Confirm `smartIdentityProviders` includes your external IDP authority URL.
  - Confirm the `audience` matches what the IDP issues in the token.

- **Postman / REST client configuration:**
  - All environment variables contain proper values.
  - The `resource` variable matches the FHIR Server audience.

### FHIR Authentication Configuration Issues

If the FHIR service does not accept tokens from your external IDP:

- Open the FHIR service in Azure Portal → **Settings** → **Authentication**.
- Verify:
  - **Authority** is set to `https://login.microsoftonline.com/<tenant-id>`.
  - **Audience** matches the FHIR service URL (or your custom audience).
  - **Identity Provider 1** shows your external IDP authority URL under `smartIdentityProviders`.
- If `smartIdentityProviders` is missing, verify the `AuthorityURL` was set correctly before `azd up` and redeploy.

### Not Getting Correct Scopes

If you are getting more scopes assigned to your token than expected:

- Ensure you have **not** applied admin consent for any scopes. Admin consent overrides user-selected scopes.
- In your external IDP, verify that scope consent is configured at the user level, not the admin level.

---

## Deployment Issues

### Azure Developer CLI Deployment Failures

The Azure Developer CLI creates a deployment in Azure as part of the `azd up` command. To get additional details:

1. Open the Azure Portal and navigate to the resource group (`{env-name}-rg`).
2. Go to **Deployments** in the left menu.
3. A single `azd up` command spawns multiple child deployments — click into the failing deployment for specifics.
4. Check the **Error details** tab for the root cause.

Common deployment issues:
- **Not enough permissions** — Ensure your Azure account has **Owner** privileges on the subscription.
- **Resource providers not registered** — Register required providers: `Microsoft.HealthcareApis`, `Microsoft.Cache`, `Microsoft.Web`, `Microsoft.Insights`.
- **Environment name too long** — Must be 18 characters or fewer, lowercase alphanumeric only.
- **Region not supported** — Ensure the selected Azure region supports all required resource types (FHIR, Redis, Functions).

### PowerShell Script Issues

If you encounter an error while running PowerShell scripts:

```
Script cannot be loaded. The script is not digitally signed. You cannot run this script on the current system.
```

Temporarily bypass the execution policy:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

This allows you to run the script without altering the execution policy permanently. For more information, see [about_Execution_Policies](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies).

### Redeployment After Configuration Changes

If you need to fix a configuration value (e.g., `AuthorityURL`) after initial deployment:

1. Update the environment value:
   ```powershell
   azd env set AuthorityURL "https://corrected-idp-url.com/oauth2/default"
   ```
2. Redeploy:
   ```powershell
   azd up
   ```

---

## Redis Connectivity Issues

If the Function App cannot connect to Redis:

- Verify the `AZURE_CacheConnectionString` app setting is present in the Function App.
- In the Azure Portal, check that the Redis cache (`{env-name}-cache`) has status **Running**.
- Redis is deployed with **TLS 1.2** minimum — ensure your connection string includes `ssl=True`.
- Check Application Insights for Redis-related dependency failures.

---

## Useful Diagnostic Tools

| Tool | Purpose |
|---|---|
| [jwt.ms](https://jwt.ms/) | Decode and inspect access tokens |
| Application Insights → **Failures** | View failed requests and dependency calls |
| Application Insights → **Transaction search** | End-to-end request tracing |
| Azure Portal → Resource Group → **Deployments** | Infrastructure deployment error details |
| `azd env get-values` | Retrieve current environment configuration |

---

**[Back to Previous Page](../README.md)**