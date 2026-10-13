# Auth Context Frontend App Registration

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](../troubleshooting.md).

This application registration is used by the sample's Entra-only browser experiences:

- the built-in EHR launch initiator, which pushes SMART context (`patient`, `encounter`, `launch`, etc.) to `/api/context-cache`; and
- the built-in consent picker, which obtains a token before calling `/api/appConsentInfo`.

---

## Steps

### 1. Create the application registration

1. Sign in to the **Microsoft Entra admin center** in your tenant.
2. Navigate to **Microsoft Entra ID → App registrations → New registration**.
3. Set the following values:
   - **Name**: a descriptive name such as `<env-name>-auth-context-frontend`.
   - **Supported account types**: *Accounts in this organizational directory only*.
   - **Redirect URI**: select **Single-page application (SPA)** and enter `http://localhost:3000` for local debugging or EHR application from where you will be launching smart app.
4. Click **Register**.
5. Record the **Application (client) ID** — this is `ContextAppClientId`.

> [!IMPORTANT]
> Add both deployed gateway pages as **Single-page application (SPA)** redirect URIs:
>
> - `https://<your-gateway-host>/api/ehr-launch`
> - `https://<your-gateway-host>/api/consent-ui`
>
> The gateway host is the `FunctionBaseUrl` value in `.azure/<env>/.env`. The first URI supports the built-in EHR launch initiator used by the Inferno **EHR Practitioner App** test. The second supports the built-in Entra consent picker. MSAL redirect sign-in fails if either page is used without its matching redirect URI.

### 2. Tell `azd` about the application

From the repository root:

```powershell
azd env set ContextAppClientId "<application-id-from-step-1>"
```

This value is consumed by the gateway as `AZURE_ContextAppClientId` and used to validate the caller of `/api/context-cache`.

### 3. Grant API permissions (if your launch initiator requires them)

If your EHR launch initiator signs the user in to acquire a token before posting context, grant it `User.Read` on Microsoft Graph (or whichever scopes your initiator requires). The default `User.Read` is usually sufficient for the sample's flow.

---

## Outcome

When you finish you will have:

- `ContextAppClientId` set on the `azd` environment.
- A SPA-style app registration with the local-development URI and both deployed gateway redirect URIs.
- The gateway configured to authorize callers of `/api/context-cache` against this `client_id`.
- The consent picker configured to acquire a token for `/api/appConsentInfo`.

After `azd up`, also complete the mandatory managed-identity Graph permission step in [Deploy with Microsoft Entra ID](../deployment-entra.md#grant-microsoft-graph-permissions-to-the-gateway).

[Back to Microsoft Entra ID Deployment](../deployment-entra.md)
