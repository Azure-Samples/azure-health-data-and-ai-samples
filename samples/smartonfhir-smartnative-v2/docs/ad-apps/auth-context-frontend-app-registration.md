# Auth Context Frontend App Registration

> [!TIP]
> If you encounter any issues during configuration, deployment, or testing, please refer to the [Troubleshooting Guide](../troubleshooting.md).

This application registration represents the **EHR launch context delivery client** — the SPA (or service) that the EHR uses to push SMART launch context (`patient`, `encounter`, `launch`, etc.) into the gateway's `/api/context-cache` endpoint before the SMART app exchanges its launch code for a token.

This is required only when deploying with `IdpType=EntraId`. External IdPs handle launch context delivery through their own authorization servers and the gateway accepts it directly from the upstream IdP token response.

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
- A SPA-style app registration with both the local-dev and the deployed gateway redirect URIs.
- The gateway configured to authorize callers of `/api/context-cache` against this `client_id`.

[Back to Microsoft Entra ID Deployment](../deployment-entra.md)
