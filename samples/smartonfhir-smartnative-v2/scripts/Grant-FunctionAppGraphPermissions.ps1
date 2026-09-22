<#
.SYNOPSIS
    Grants Microsoft Graph application permissions to the auth Function App's system-assigned
    managed identity so it can read the caller app registration and rewrite user consent grants.

.DESCRIPTION
    The consent picker (Entra IdP mode only) calls Microsoft Graph as the Function App to:
      - read the requesting application registration and its resource service principals
      - read the user's existing oauth2PermissionGrants
      - update or delete a user's grant when the picker submits a narrower scope selection

    Required Microsoft Graph app roles (application permissions):
      - Application.Read.All             (id: 9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30)
      - DelegatedPermissionGrant.ReadWrite.All (id: 8e8e4742-1d95-4f68-9d56-6ee75648c72a)

    This is a post-deploy step because Bicep for the current project does not use the
    Microsoft Graph Bicep extension. Run this once per environment (Entra mode only)
    after `azd up` succeeds. Idempotent: existing role assignments are skipped.

.PARAMETER FunctionAppPrincipalId
    Object (principal) ID of the Function App's system-assigned managed identity.
    If omitted, read from the active `azd` environment as `FunctionAppManagedIdentityPrincipalId`.

.PARAMETER TenantId
    Entra tenant id. If omitted, read from the active `azd` environment as `TenantId`,
    then from the logged-in Azure CLI account as a fallback.
#>
param (
    [Parameter(Mandatory = $false)]
    [string]$FunctionAppPrincipalId,

    [Parameter(Mandatory = $false)]
    [string]$TenantId
)

$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$SAMPLE_ROOT = (Get-Item $SCRIPT_PATH).Parent.FullName

# Fill missing params from active azd env
if ([string]::IsNullOrWhiteSpace($FunctionAppPrincipalId) -or [string]::IsNullOrWhiteSpace($TenantId)) {
    Write-Host "Reading values from azd environment..."
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    foreach ($line in $AZD_ENVIRONMENT) {
        $name, $value = $line.Split('=', 2)
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) { continue }
        $value = $value.Trim('"')
        if ([string]::IsNullOrWhiteSpace($FunctionAppPrincipalId) -and $name -eq 'FunctionAppManagedIdentityPrincipalId') {
            $FunctionAppPrincipalId = $value
        }
        if ([string]::IsNullOrWhiteSpace($TenantId) -and $name -eq 'TenantId') {
            $TenantId = $value
        }
    }
}

if ([string]::IsNullOrWhiteSpace($TenantId)) {
    $ACCOUNT = ConvertFrom-Json "$(az account show -o json)"
    $TenantId = $ACCOUNT.tenantId
}

if ([string]::IsNullOrWhiteSpace($FunctionAppPrincipalId)) {
    Write-Error "FunctionAppPrincipalId not provided and not found in azd environment. Exiting."
    exit 1
}

Write-Host "Granting Microsoft Graph app permissions to principal $FunctionAppPrincipalId in tenant $TenantId"

# Microsoft Graph application id (constant across tenants)
$graphAppId = '00000003-0000-0000-c000-000000000000'
$graphSpQuery = az ad sp list --filter "appId eq '$graphAppId'" --query "[0].id" -o tsv
if ([string]::IsNullOrWhiteSpace($graphSpQuery)) {
    Write-Error "Could not find the Microsoft Graph service principal in tenant $TenantId."
    exit 1
}
$graphSpId = $graphSpQuery.Trim()

$requiredRoles = @(
    @{ Name = 'Application.Read.All'; Id = '9a5d68dd-52b0-4cc2-bd40-abcf44ac3a30' },
    @{ Name = 'DelegatedPermissionGrant.ReadWrite.All'; Id = '8e8e4742-1d95-4f68-9d56-6ee75648c72a' }
)

$existingAssignmentsJson = az rest --method GET `
    --url "https://graph.microsoft.com/v1.0/servicePrincipals/$FunctionAppPrincipalId/appRoleAssignments" `
    -o json
$existingAssignments = ($existingAssignmentsJson | ConvertFrom-Json).value

foreach ($role in $requiredRoles) {
    $already = $existingAssignments | Where-Object { $_.appRoleId -eq $role.Id -and $_.resourceId -eq $graphSpId }
    if ($already) {
        Write-Host "  [skip] $($role.Name) already granted."
        continue
    }

    $body = @{
        principalId = $FunctionAppPrincipalId
        resourceId  = $graphSpId
        appRoleId   = $role.Id
    } | ConvertTo-Json -Compress

    # Write body to a temp file to sidestep the PowerShell/az CLI JSON quoting bug
    # (raw --body strings get their quotes stripped and Graph returns 400 BadRequest).
    $bodyFile = New-TemporaryFile
    try {
        Set-Content -Path $bodyFile -Value $body -Encoding utf8 -NoNewline
        Write-Host "  [add ] Granting $($role.Name)..."
        az rest --method POST `
            --url "https://graph.microsoft.com/v1.0/servicePrincipals/$FunctionAppPrincipalId/appRoleAssignments" `
            --headers "Content-Type=application/json" `
            --body "@$($bodyFile.FullName)" | Out-Null

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to grant $($role.Name). See error above."
            exit 1
        }
    }
    finally {
        Remove-Item $bodyFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Done. Microsoft Graph permissions granted. Verify in the portal: Enterprise applications -> the Function App MI -> Permissions."
