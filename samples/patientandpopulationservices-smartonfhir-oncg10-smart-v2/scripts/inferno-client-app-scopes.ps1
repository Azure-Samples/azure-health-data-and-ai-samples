param (
    [Parameter(Mandatory = $true)]
    [string]$ClientAppId,
    
    [Parameter(Mandatory = $true)]
    [string]$ClientType,  # "patient", "ehr", or "backend"

    [Parameter(Mandatory = $true)]
    [string]$FhirResourceAppId
)

Write-Host "`nFetching scopes and roles from FHIRResourceApp: $FhirResourceAppId"
$fhirApp = az ad app show --id $FhirResourceAppId | ConvertFrom-Json

$scopes = $fhirApp.api.oauth2PermissionScopes
$appRoles = $fhirApp.appRoles

if (-not $scopes -and -not $appRoles) {
    Write-Error "No scopes or roles found in FHIRResourceApp. Exiting..."
    exit
}

$filteredPermissions = @()
$graphPermissions = @()

# Predefined Graph App ID
$graphAppId = "00000003-0000-0000-c000-000000000000"
$graphScopeDefinitions = @(
    @{ id = "37f7f235-527c-4136-accd-4a02d197296e"; value = "openid"; type = "Scope" },
    @{ id = "7427e0e9-2fba-42fe-b0c0-848c9e6a8182"; value = "offline_access"; type = "Scope" }
)

function Get-GraphPermissions {
    try {
        $graphSp = az ad sp show --id $graphAppId | ConvertFrom-Json
        if ($null -eq $graphSp.app.oauth2PermissionScopes) {
            Write-Warning "Microsoft Graph scopes not retrieved. Using fallback list."
            return $graphScopeDefinitions
        } else {
            return $graphSp.app.oauth2PermissionScopes | ForEach-Object {
                @{ id = $_.id; value = $_.value; type = "Scope" }
            }
        }
    } catch {
        Write-Warning "Failed to fetch Microsoft Graph permissions. Using hardcoded fallback list."
        return $graphScopeDefinitions
    }
}

$addedPermissions = @{}
$clientKey = $ClientType.ToLower()

# Define fixed and wildcard permissions per client type
$fixedPermissionsMap = @{
    "patient" = @("fhirUser", "launch.patient")
    "ehr"     = @("fhirUser", "launch")
}
$wildcardPatternsMap = @{
    "patient" = "patient.*"
    "ehr"     = "user.*"
}

switch ($clientKey) {
    "patient" {
        $pattern = $wildcardPatternsMap[$clientKey]
        $filteredPermissions += $scopes | Where-Object { $_.value -like $pattern } | ForEach-Object {
            @{ id = $_.id; value = $_.value; type = "Scope" }
        }

        $fixedPermissionsMap[$clientKey] | ForEach-Object {
            $perm = $_
            $match = $scopes | Where-Object { $_.value -eq $perm } | Select-Object -First 1
            if ($match -and -not $addedPermissions.ContainsKey($perm)) {
                $filteredPermissions += @{ id = $match.id; value = $match.value; type = "Scope" }
                $addedPermissions[$perm] = $true
            }
        }

        $graphPermissions = Get-GraphPermissions
    }

    "ehr" {
        $pattern = $wildcardPatternsMap[$clientKey]
        $filteredPermissions += $scopes | Where-Object { $_.value -like $pattern } | ForEach-Object {
            @{ id = $_.id; value = $_.value; type = "Scope" }
        }

        $fixedPermissionsMap[$clientKey] | ForEach-Object {
            $perm = $_
            $match = $scopes | Where-Object { $_.value -eq $perm } | Select-Object -First 1
            if ($match -and -not $addedPermissions.ContainsKey($perm)) {
                $filteredPermissions += @{ id = $match.id; value = $match.value; type = "Scope" }
                $addedPermissions[$perm] = $true
            }
        }

        $graphPermissions = Get-GraphPermissions
    }

    "backend" {
        foreach ($role in $appRoles) {
            if ($role.value -eq "user.all.read") {
                Write-Host $role.value
                $filteredPermissions += @{ id = $role.id; value = $role.value; type = "Role" }
                break
            }
        }
    }

    default {
        Write-Error "Invalid ClientType. Use 'patient', 'ehr', or 'backend'."
        exit
    }
}

Write-Host "`nApplying permissions for Client Type: $ClientType"


# Filter out any entries missing id or type
# Remove unwanted permissions
if($clientKey -ne "backend"){
    $filteredPermissions = $filteredPermissions | Where-Object {
        $clientKey -ne "backend" -and $_.value -notlike "*.all.rs*" -and $_.value -notlike "*.all.read*"
    }
}else{
    $filteredPermissions = $filteredPermissions | Where-Object {
        $_.value -eq "user.all.read"
    }
}

$graphPermissions = $graphPermissions | Where-Object { $_.id -and $_.type }

# Group and batch permissions
$apiPermissions = ($filteredPermissions | ForEach-Object {
    "$($_.id)=$($_.type)"
}) -join " "

Write-Host "FHIR Permissions: $apiPermissions"

# Apply FHIR permissions
if ($apiPermissions -and $apiPermissions.Trim() -ne "") {
    Write-Host "`nAdding all FHIR permissions in one call..."
    $fhirCommand = "az ad app permission add --id $ClientAppId --api $FhirResourceAppId --api-permissions $apiPermissions"
    Write-Host "Executing: $fhirCommand"
    Invoke-Expression $fhirCommand
} else {
    Write-Warning "No valid FHIR permissions to apply."
}

# Apply Microsoft Graph permissions
foreach ($gperm in $graphPermissions) {
    Write-Host "Adding Microsoft Graph permission: $($gperm.value) ($($gperm.id)) as $($gperm.type)"
    az ad app permission add `
        --id $ClientAppId `
        --api $graphAppId `
        --api-permissions "$($gperm.id)=$($gperm.type)"
}


Write-Host "`nPermissions successfully applied to the Client App!"
