# Summary:
# This script, preDeploymentScript.ps1, is used to deploy the necessary security artifacts for the Saas version of HDS in Purview solution.
# The script automates the creation and configuration of several Azure resources, ensuring they are set up correctly for the solution to function.
# The resources deployed by this script include:
# 1. App Registration: An Azure App Registration is created. This represents the application in the Azure AD and allows it to authenticate and be authorized for access to other resources.
# 2. Resource Group: An Azure Resource Group is created. This is a container that holds related resources for an Azure solution. The resource group includes those resources that you want to manage as a group.
# 3. Managed Identity: An Azure Managed Identity is created. This is an identity in Azure Active Directory, and it is automatically managed by Azure. Managed identities eliminate the need for developers having to manage credentials.
# The script ensures that these resources are created if they do not exist, and configures them with the necessary permissions and settings for the PurviewHealthcareKit solution.

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]$AppRegistrationName,

    [Parameter(Mandatory = $true)]
    [string]$ManagedIdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $true)]
    [string]$SubscriptionId,

    [Parameter(Mandatory = $false)]
    [switch]$GrantAppAdminConsent
)

#Get current date time in UTC format for logging purposes
function Get-CurrentDateTime {
    return (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss') + 'Z'
} 

# Check PowerShell version
if (-not ($PSVersionTable.PSVersion.Major -eq 5 -and $PSVersionTable.PSVersion.Minor -eq 1)) {
    Write-Error "[$(Get-CurrentDateTime)]: PowerShell version 5.1 is required."
    exit
}

if ($GrantAppAdminConsent) {
    $azCommand = Get-Command az -ErrorAction SilentlyContinue
    if (-not $azCommand) {
        Write-Error "[$(Get-CurrentDateTime)]: The 'az' command is not installed. Please install Azure CLI to proceed. Instructions are available at https://learn.microsoft.com/en-us/cli/azure/install-azure-cli-windows?tabs=azure-cli"
        exit
    }
}

# Define an array of required modules
$requiredModules = @('Az.Accounts', 'Az.ManagedServiceIdentity', 'Az.Resources', 'AzureAD')

foreach ($module in $requiredModules) {
    # Check if the module is installed, if not, install it
    if (-not (Get-Module -ListAvailable -Name $module)) {
        Write-Host "[$(Get-CurrentDateTime)]: $module module not found. Installing..."
        Install-Module -Name $module -Scope CurrentUser -Force -AllowClobber
        Import-Module $module
    }
    else {
        Write-Host "[$(Get-CurrentDateTime)]: $module module is already installed."
    }
}

# Function to enable a role if it's not already enabled
function Enable-Role {
    param(
        [string] $permission
    )

    # Get the role with the specified permission
    $role = Get-AzureADDirectoryRole | Where-Object { $_.DisplayName -eq $permission }

    # If the role is null, enable it
    if ($null -eq $role) {
        $roleTemplate = Get-AzureADDirectoryRoleTemplate | Where-Object { $_.DisplayName -eq $permission }
        $role = Enable-AzureADDirectoryRole -RoleTemplateId $roleTemplate.ObjectId
    }
    return $role
}

# Function to assign permissions to an identity
function Grant-Permissions {
    param(
        [string] $identityId,
        [string[]] $permissions
    )

    # For each permission, enable the role and add the identity to it
    foreach ($permission in $permissions) {
        $role = Enable-Role -permission $permission
        Write-Verbose "Role: $permission,  id: $identityId and role id: $role.ObjectId"
        
        $checkRoleMember = Get-AzureADDirectoryRoleMember -ObjectId $role.ObjectId | Where-Object { $_.ObjectId -eq $identityId }

        if ($null -eq $checkRoleMember) {
            Write-Host "[$(Get-CurrentDateTime)]: Role member does not exist, adding member..."
            Start-Sleep -Seconds $sleepDurationSeconds
            $identityDetails = Get-AzADServicePrincipal -ObjectId $identityId -ErrorAction SilentlyContinue

            for ($identityLoop = 0; $identityLoop -lt $retryAttempts; $identityLoop++) {
                if ($null -ne $identityDetails) {
                    break
                }
                Write-Host "[$(Get-CurrentDateTime)]: Unable to retrieve principal id, retrying..."
                Start-Sleep -Seconds $sleepDurationSeconds
                $identityDetails = Get-AzADServicePrincipal -ObjectId $identityId -ErrorAction SilentlyContinue
            }
            if ($null -eq $identityDetails) {
                Write-Error "[$(Get-CurrentDateTime)]: Failed to retrieve principal id."
                exit
            }
            Add-AzureADDirectoryRoleMember -ObjectId $role.ObjectId -RefObjectId $identityId 
            
        }
        else {
            Write-Host "[$(Get-CurrentDateTime)]: Role member exists"
        }
        
    }
    Write-Host "[$(Get-CurrentDateTime)]: Permissions assigned successfully"
}


$sleepDurationSeconds = 5
# This retry count is used to retry for 3 minutes. 36 x 5 = 180 seconds.
$retryAttempts = 36
$ErrorActionPreference = 'Stop'

## manual steps not possible via bicep templates and user assigned managed identity
$managedIdentityPermissions = @('Cloud Application Administrator')
$appRegistrationPermissions = @('Exchange Administrator', 'Compliance Administrator')
# Define the API and permissions for the Office 365 Exchange Online API
$appRegistrationAPIAppId = '00000002-0000-0ff1-ce00-000000000000'
$appRegistrationAPIPermissionsId = 'dc50a0fb-09a3-484d-be87-e023b12c6440'

# glossary api permissions
$appRegistrationGlossaryAPIAppId = '73c2949e-da2d-457a-9607-fcc665198967' #Microsoft Purview API
$appRegistrationGlossaryAPIPermissionsId = '8d48872e-7710-4001-bfd0-7dac15c28f69' #Purview Application API Access

Connect-AzAccount -Subscription $SubscriptionId

$subscriptionDetails = Get-AzSubscription -SubscriptionId $SubscriptionId
$tenantId = $subscriptionDetails.TenantId
Write-Host "Tenant ID: $tenantId"

# Get the resource group with the specified name
$resourceGroup = Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue
# If the resource group is null, create it
if ($null -eq $resourceGroup) {
    Write-Host "[$(Get-CurrentDateTime)]: Creating Resource Group with Name: $ResourceGroupName"
    $resourceGroup = New-AzResourceGroup -Name $ResourceGroupName -Location $Location
}
Write-Host "[$(Get-CurrentDateTime)]: Using Resource Group with Name: $($resourceGroup.ResourceGroupName)"

Connect-AzureAD -TenantId $tenantId | Out-Null

# Get the Azure AD application with the specified name
$azureAdApplication = Get-AzADApplication -DisplayName $AppRegistrationName -ErrorAction SilentlyContinue
# If the Azure AD application is null, create it
if ($null -eq $azureAdApplication) {
    Write-Host "[$(Get-CurrentDateTime)]: Creating App Registration with Name: $AppRegistrationName"
    $azureAdApplication = New-AzADApplication -DisplayName $AppRegistrationName
}
Write-Host "[$(Get-CurrentDateTime)]: Using App Registration with ID: $($azureAdApplication.Id)"

for ($appRegLoopCount = 0; $appRegLoopCount -lt $retryAttempts; $appRegLoopCount++) {
    if ($null -ne $azureAdApplication) {
        break
    }
    Write-Host "[$(Get-CurrentDateTime)]: Waiting for App Registration to be created..."
    Start-Sleep -Seconds $sleepDurationSeconds
    $azureAdApplication = Get-AzADApplication -DisplayName $AppRegistrationName -ErrorAction SilentlyContinue
}
if ( $null -eq $azureAdApplication) {
    Write-Error "[$(Get-CurrentDateTime)]: Failed to create App Registration."
    exit
}
#check permissions for the app registration
$app = Get-AzADAppPermission -ObjectId $azureAdApplication.Id -ErrorAction SilentlyContinue

# # Filter to find specific permission ID in RequiredResourceAccess
$matchingPermissions = $app | Where-Object {
    $_.ApiId -eq $appRegistrationAPIAppId
}

if ($null -eq $matchingPermissions) {
    Write-Host "[$(Get-CurrentDateTime)]: Add the Office 365 Exchange Online ($appRegistrationAPIAppId) Exchange.ManageAsApp ($appRegistrationAPIPermissionsId) API permission to the app registration"
    Add-AzADAppPermission -ObjectId $azureAdApplication.Id -ApiId $appRegistrationAPIAppId -PermissionId $appRegistrationAPIPermissionsId -Type Role -ErrorAction Stop
}
else {
    Write-Host "[$(Get-CurrentDateTime)]: Office 365 Exchange Online ($appRegistrationAPIAppId) Exchange.ManageAsApp ($appRegistrationAPIPermissionsId) API permission exists in the app registration"
}

# # Glossaries Filter to find specific permission ID in RequiredResourceAccess
$matchingPermissions = $app | Where-Object {
    $_.ApiId -eq $appRegistrationGlossaryAPIAppId
}

if ($null -eq $matchingPermissions) {
    Write-Host "[$(Get-CurrentDateTime)]: Add the Microsoft Purview API ($appRegistrationGlossaryAPIAppId) Purview Application API Access ($appRegistrationGlossaryAPIPermissionsId) API permission to the app registration"
    Add-AzADAppPermission -ObjectId $azureAdApplication.Id -ApiId $appRegistrationGlossaryAPIAppId -PermissionId $appRegistrationGlossaryAPIPermissionsId -Type Role -ErrorAction Stop
}
else {
    Write-Host "[$(Get-CurrentDateTime)]: Microsoft Purview API ($appRegistrationGlossaryAPIAppId) Purview Application API Access ($appRegistrationGlossaryAPIPermissionsId) API permission exists in the app registration"
}

# Get the managed identity with the specified name
Write-Host "[$(Get-CurrentDateTime)]: Checking if Managed Identity exists with Name: $ManagedIdentityName"
$createdManagedIdentity = Get-AzUserAssignedIdentity -ResourceGroupName $ResourceGroupName -Name $ManagedIdentityName -ErrorAction SilentlyContinue
#if the managed identity is null, create it
if ($null -eq $createdManagedIdentity) {
    Write-Host "[$(Get-CurrentDateTime)]: Creating Managed Identity with Name: $ManagedIdentityName"
    $createdManagedIdentity = New-AzUserAssignedIdentity -ResourceGroupName $ResourceGroupName -Name $ManagedIdentityName -Location $Location
}
Write-Host "[$(Get-CurrentDateTime)]: Using Managed Identity with ID: $($createdManagedIdentity.PrincipalId)"

for ($miLoopCount = 0; $miLoopCount -lt $retryAttempts; $miLoopCount++) {
    if ($null -ne $createdManagedIdentity) {
        break
    }
    Write-Host "[$(Get-CurrentDateTime)]: Waiting for Managed Identity to be created..."
    Start-Sleep -Seconds $sleepDurationSeconds
    $createdManagedIdentity = Get-AzUserAssignedIdentity -ResourceGroupName $ResourceGroupName -Name $ManagedIdentityName -ErrorAction SilentlyContinue
}
if ( $null -eq $createdManagedIdentity) {
    Write-Error "[$(Get-CurrentDateTime)]: Failed to create Managed Identity."
    exit
}

Write-Host "[$(Get-CurrentDateTime)]: Assign permissions to the Managed Identity"
Grant-Permissions -identityId $createdManagedIdentity.PrincipalId -permissions $managedIdentityPermissions

Write-Host "[$(Get-CurrentDateTime)]: Assign permissions to the App Registration"
$servicePrincipalForApp = Get-AzADServicePrincipal -ApplicationId $azureAdApplication.AppId -ErrorAction SilentlyContinue

for ($spLoopCount = 0; $spLoopCount -lt $retryAttempts; $spLoopCount++) {
    if ($null -ne $servicePrincipalForApp) {
        break
    }
    Write-Host "[$(Get-CurrentDateTime)]: Waiting for Service Principal to be created..."
    Start-Sleep -Seconds $sleepDurationSeconds
    $servicePrincipalForApp = New-AzADServicePrincipal -ApplicationId $azureAdApplication.AppId -ErrorAction SilentlyContinue
}
if ( $null -eq $servicePrincipalForApp) {
    Write-Error "[$(Get-CurrentDateTime)]: Failed to create Service Principal for App Registration."
    exit
}

Grant-Permissions -identityId $servicePrincipalForApp.Id -permissions $appRegistrationPermissions 


if ($GrantAppAdminConsent) {
    Write-Host "[$(Get-CurrentDateTime)]: Granting Admin Consent to the App Registration"
    az login --tenant $TenantId
    az ad app permission admin-consent --id $azureAdApplication.Id
    Write-Host "[$(Get-CurrentDateTime)]: Granted Admin Consent to the App Registration"
}
else {
    Write-Host "[$(Get-CurrentDateTime)]: An Entra ID global administrator will need to grant admin consent to the App Registration with ID: $($azureAdApplication.Id)"
    Read-Host -Prompt "Granting admin consent can be performed after this script is completed. Press Enter to continue script execution."
}

Write-Host "[$(Get-CurrentDateTime)]: Pre-Deployment script completed successfully. Here are the details of the resources created:"
Write-Host "Resource group: $($resourceGroup.ResourceGroupName)`nService principal: $($azureAdApplication.DisplayName)`nUser assigned managed identity: $($createdManagedIdentity.Name)`nLocation: $($resourceGroup.Location)"

if (!$GrantAppAdminConsent) {
    Write-Host "Please ensure that the App Registration has been granted Admin Consent by an Entra ID global administrator."
}
