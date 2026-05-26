<#
    Setup-SmartEnvironment.ps1
    
    Creates all Entra ID app registrations needed for SMART on FHIR testing:
    1. FHIR Resource App Registration (defines SMART scopes)
    2. Test Client App Registration (public client for testing)
    
    Prerequisites:
    - Azure CLI installed and logged in (az login)
    - Permissions to create app registrations in your tenant
    
    Usage:
    .\Setup-SmartEnvironment.ps1
#>

$ErrorActionPreference = "Stop"

# Get current tenant info
Write-Host "Getting tenant information..." -ForegroundColor Cyan
$ACCOUNT = az account show -o json | ConvertFrom-Json
$TENANT_ID = $ACCOUNT.tenantId
Write-Host "Tenant: $($ACCOUNT.name) ($TENANT_ID)" -ForegroundColor Green

$DOMAIN_INFO = az rest --method get --url 'https://graph.microsoft.com/v1.0/domains?$select=id' | ConvertFrom-Json
$PRIMARY_DOMAIN = $DOMAIN_INFO.value[0].id
Write-Host "Domain: $PRIMARY_DOMAIN" -ForegroundColor Green

# =====================================================
# 1. Create FHIR Resource App Registration
# =====================================================
Write-Host ""
Write-Host "=== Step 1: Creating FHIR Resource App Registration ===" -ForegroundColor Yellow

$FHIR_APP_NAME = "smart-fhir-resource"

# Check if already exists
$EXISTING_FHIR_APP = az ad app list --display-name $FHIR_APP_NAME --query "[0].appId" -o tsv 2>$null
if ($EXISTING_FHIR_APP) {
    Write-Host "FHIR Resource App already exists: $EXISTING_FHIR_APP" -ForegroundColor Yellow
    $FHIR_APP_ID = $EXISTING_FHIR_APP
} else {
    # Create the app
    $FHIR_APP = az ad app create --display-name $FHIR_APP_NAME -o json | ConvertFrom-Json
    $FHIR_APP_ID = $FHIR_APP.appId
    Write-Host "Created FHIR Resource App: $FHIR_APP_ID" -ForegroundColor Green
}

# Set identifier URI (this becomes FhirAudience)
$FHIR_AUDIENCE = "https://$FHIR_APP_NAME.$PRIMARY_DOMAIN"
Write-Host "Setting identifier URI: $FHIR_AUDIENCE" -ForegroundColor Cyan

az ad app update --id $FHIR_APP_ID --identifier-uris $FHIR_AUDIENCE

# Add SMART scopes (oauth2PermissionScopes) and app roles
$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition

# Check if manifest files exist in the expected locations
$AppRolesPath = Join-Path $SCRIPT_PATH "..\scripts\manifest-json-contents\app-roles.json"
$OAuth2PermissionsPath = Join-Path $SCRIPT_PATH "..\scripts\manifest-json-contents\oauth2-permissions.json"

if (-not (Test-Path $AppRolesPath)) {
    $AppRolesPath = Join-Path $SCRIPT_PATH "..\..\scripts\manifest-json-contents\app-roles.json"
    $OAuth2PermissionsPath = Join-Path $SCRIPT_PATH "..\..\scripts\manifest-json-contents\oauth2-permissions.json"
}

if (Test-Path $AppRolesPath) {
    Write-Host "Configuring SMART scopes and app roles..." -ForegroundColor Cyan
    az ad app update --id $FHIR_APP_ID --set appRoles=@$AppRolesPath api=@$OAuth2PermissionsPath
    Write-Host "SMART scopes configured." -ForegroundColor Green
} else {
    Write-Host "WARNING: Could not find manifest files at $AppRolesPath" -ForegroundColor Red
    Write-Host "You'll need to run Configure-FhirResourceAppRegistration.ps1 separately" -ForegroundColor Red
}

# Enable acceptMappedClaims
az ad app update --id $FHIR_APP_ID --set api.acceptMappedClaims=true

# =====================================================
# 2. Create Test Client App Registration (Public Client)
# =====================================================
Write-Host ""
Write-Host "=== Step 2: Creating Test Client App Registration ===" -ForegroundColor Yellow

$CLIENT_APP_NAME = "smart-test-client"
$REDIRECT_URI = "https://localhost:7244/callback"
# Our auth-proxy callback. Required on every SMART client's Entra app registration so Entra
# will redirect EHR-launch flows back to this server for launch-context resolution.
$PROXY_CALLBACK_URI = "https://localhost:7244/auth/proxy-callback"

# Check if already exists
$EXISTING_CLIENT_APP = az ad app list --display-name $CLIENT_APP_NAME --query "[0].appId" -o tsv 2>$null
if ($EXISTING_CLIENT_APP) {
    Write-Host "Test Client App already exists: $EXISTING_CLIENT_APP" -ForegroundColor Yellow
    $CLIENT_APP_ID = $EXISTING_CLIENT_APP
} else {
    # Create public client app
    $CLIENT_APP = az ad app create `
        --display-name $CLIENT_APP_NAME `
        --public-client-redirect-uris $REDIRECT_URI "http://localhost:5113/callback" $PROXY_CALLBACK_URI `
        --sign-in-audience AzureADMyOrg `
        -o json | ConvertFrom-Json
    $CLIENT_APP_ID = $CLIENT_APP.appId
    Write-Host "Created Test Client App: $CLIENT_APP_ID" -ForegroundColor Green
}

# Ensure the proxy-callback URI is present on the public client redirect URI list (idempotent).
Write-Host "Ensuring proxy-callback URI is registered: $PROXY_CALLBACK_URI" -ForegroundColor Cyan
$EXISTING_URIS = az ad app show --id $CLIENT_APP_ID --query "publicClient.redirectUris" -o json | ConvertFrom-Json
$URI_SET = @($EXISTING_URIS)
if ($URI_SET -notcontains $PROXY_CALLBACK_URI) { $URI_SET += $PROXY_CALLBACK_URI }
if ($URI_SET -notcontains $REDIRECT_URI)       { $URI_SET += $REDIRECT_URI }
if ($URI_SET -notcontains "http://localhost:5113/callback") { $URI_SET += "http://localhost:5113/callback" }
az ad app update --id $CLIENT_APP_ID --public-client-redirect-uris @($URI_SET) | Out-Null

# Grant API permissions to the FHIR Resource App (user_impersonation scope)
Write-Host "Granting API permissions..." -ForegroundColor Cyan
$FHIR_APP_OBJECT = az ad app show --id $FHIR_APP_ID -o json | ConvertFrom-Json
$USER_IMPERSONATION_SCOPE = ($FHIR_APP_OBJECT.api.oauth2PermissionScopes | Where-Object { $_.value -eq "user_impersonation" }).id

if ($USER_IMPERSONATION_SCOPE) {
    az ad app permission add --id $CLIENT_APP_ID --api $FHIR_APP_ID --api-permissions "$USER_IMPERSONATION_SCOPE=Scope" 2>$null
    Write-Host "API permissions granted." -ForegroundColor Green
} else {
    Write-Host "NOTE: No user_impersonation scope found. You may need to add permissions manually." -ForegroundColor Yellow
}

# =====================================================
# 3. Output configuration
# =====================================================
Write-Host ""
Write-Host "=============================================" -ForegroundColor Green
Write-Host "  SETUP COMPLETE" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Green
Write-Host ""
Write-Host "Copy these values to your appsettings.json:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  SmartConfig:" -ForegroundColor White
Write-Host "    FhirServerUrl:  https://gksmartws-smartnative.fhir.azurehealthcareapis.com" -ForegroundColor Cyan
Write-Host "    AuthorityUrl:   https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0" -ForegroundColor Cyan
Write-Host "    Audience:       https://localhost:7244/smart" -ForegroundColor Cyan
Write-Host "    FhirAudience:   $FHIR_AUDIENCE" -ForegroundColor Cyan
Write-Host "    ContextAppUrl:  http://localhost:3000" -ForegroundColor Cyan
Write-Host ""
Write-Host "  For testing:" -ForegroundColor Yellow
Write-Host "    TenantId:        $TENANT_ID" -ForegroundColor Cyan
Write-Host "    FHIR App ID:     $FHIR_APP_ID" -ForegroundColor Cyan
Write-Host "    FhirAudience:    $FHIR_AUDIENCE" -ForegroundColor Cyan
Write-Host "    Client App ID:   $CLIENT_APP_ID" -ForegroundColor Cyan
Write-Host "    Redirect URI:    $REDIRECT_URI" -ForegroundColor Cyan
Write-Host "    Proxy Callback:  $PROXY_CALLBACK_URI" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Test authorize URL (Standalone launch):" -ForegroundColor Yellow
Write-Host "    https://localhost:7244/auth/authorize?response_type=code&client_id=$CLIENT_APP_ID&scope=launch/patient%20patient/Patient.rs%20openid&redirect_uri=$([uri]::EscapeDataString($REDIRECT_URI))&aud=https://localhost:7244/smart&state=test123" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Test authorize URL (EHR launch \u2014 launch token is base64 of {""patient"":""<id>""}):" -ForegroundColor Yellow
$SAMPLE_LAUNCH = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes('{"patient":"YOUR_PATIENT_ID","encounter":"YOUR_ENCOUNTER_ID"}'))
Write-Host "    https://localhost:7244/auth/authorize?response_type=code&client_id=$CLIENT_APP_ID&scope=launch%20patient/Patient.rs%20openid&redirect_uri=$([uri]::EscapeDataString($REDIRECT_URI))&aud=https://localhost:7244/smart&state=test123&launch=$SAMPLE_LAUNCH" -ForegroundColor Cyan
Write-Host ""
