## Deployment Issues

## 1. **Problem:** Static Web App shows “Get Started” page after deployment

**Symptoms:**
- After deploying the Azure Static Web App, the site displays the default “Get Started” page instead of the actual application.

**Cause:**

- This usually means the content wasn’t correctly deployed to the expected path in the static web app.

**Solution:**
Use the following command to explicitly deploy the correct output folder:

```bash
swa deploy ".\bin\Release\net8.0\publish\wwwroot" \
  --app-name "<static-app-name>" \
  --deployment-token "<deployment-token>" \
  --env "production"  
```
**Note:**
- Ensure that the Static Web Apps CLI is properly installed. 
```bash
npm install -g @azure/static-web-apps-cli
```

## 2. **Problem:** CORS errors when running Azure Functions locally

**Symptoms:**
- HTTP requests from the frontend to Backend Function App fail with CORS errors

**Cause:**
- The Local Function App is not configured to allow cross-origin requests from your frontend origin.

**Solution:**

- Add the following configuration to your Local Function App `local.settings.json` file to enable CORS for your local frontend:

```json
{
  "Host": {
    "CORS": "https://localhost:44321",
    "CORSCredentials": true
  }
}