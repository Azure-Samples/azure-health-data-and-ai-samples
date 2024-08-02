param (
 
    #FHIR Service Resource Id
    [Parameter(Mandatory=$true)]
    [string]$fhirServiceResourceId
)
 
function parseFhirServiceResourceId{
    param (
       [string]$resourceID
   )
   $resourceIdValuesArray = $resourceID.Split('/')
   $workspace = 0..($resourceIdValuesArray.Length -1) | Where-Object {$resourceIdValuesArray[$_] -eq 'workspaces'}
   $fhirservice = 0..($resourceIdValuesArray.Length -1) | Where-Object {$resourceIdValuesArray[$_] -eq 'fhirservices'}
 
   # Health Workspace name of FHIR Service instance
   $workspaceOutput = ''
   if(!$workspace -eq ''){
     $workspaceOutput = $resourceIdValuesArray.get($workspace+1)
   }
   else{
    Write-Host -ForegroundColor Red "Invalid Fhir Service Resource Id: Workspace name is missing"
    Exit
   }
 
   # Fhir service instance name
   $fhirserviceOutput = ''
   if(!$fhirservice -eq ''){
     $fhirserviceOutput = $resourceIdValuesArray.get($fhirservice+1)
   }
   else{
    Write-Host -ForegroundColor Red "Invalid Fhir Service Resource Id: Fhir service name is missing"
    Exit
   }
 
   $result = $workspaceOutput,$fhirserviceOutput
   return $result
}
 
try {
 
    $parseFhirServiceResourceIdOutput = parseFhirServiceResourceId -resourceID $fhirServiceResourceId
 
    # Health Workspace name of FHIR Service instance
    $WorkspaceName = $parseFhirServiceResourceIdOutput[0]
 
    # Fhir service instance name
    $FHIRServiceName = $parseFhirServiceResourceIdOutput[1]
 
    # Check Az Module and user logged in    
    Write-Host('Checking AZ module installed');
    $Azmodule_check = Get-Command az -ErrorVariable Azmodule_check -ErrorAction SilentlyContinue
    if (!$Azmodule_check) {
        Write-Host "Az CLI is not installed. Please install the az cli and re-run the script." -ForegroundColor Red
        Exit
    }
   
    # Log in the user and select subscription
    Write-Host('Log in and select the subscription');
    az login
   
    # Create new client application in Azure AD
    Write-Host('Create new client application in Azure AD');
    $randomString = ([System.Guid]::NewGuid()).ToString()
    $appregname ='app-'+$FHIRServiceName+'-'+$randomString
   
    $clientid=$(az ad app create --display-name $appregname --sign-in-audience AzureADMultipleOrgs --query appId --output tsv)
    if(!$clientid)
    {
        Write-Host -ForegroundColor Red "An error occurred: Failed to generate new client app registration"
        Exit
    }
 
    # Create new client secret
    Write-Host('Create new client secret');
    $clientsecretname="appsecret-"+$randomString
    $clientsecret=$(az ad app credential reset --id $clientid --append --display-name $clientsecretname --query password --output tsv)
    if(!$clientsecret)
    {
        Write-Host -ForegroundColor Red "An error occurred: Failed to generate new client app secret"
        Exit
    }
 
    # Create service principal
    Write-Host('Create new service principal');
    $spid=$(az ad sp create --id $clientid --query id --output tsv)
    if(!$spid)
    {
        Write-Host -ForegroundColor Red "An error occurred: Failed to generate new client app service principal"
        Exit
    }
   
    # Get the tenant ID
    Write-Host('Get the tenant id');
    $tenantId = $(az account list --query "[?isDefault].tenantId | [0]" --output tsv)
   
    # Role name
    $fhirrole="FHIR Data Contributor"
   
    # Assign FHIR Contributor role to the client application in FHIR service instance
    Write-Host('Assign Fhir Data Contributor role to Fhir service instance');
    $output = az role assignment create --assignee-object-id $spid --assignee-principal-type ServicePrincipal --role "$fhirrole" --scope $fhirServiceResourceId
    if(!$output)
    {
        Write-Host -ForegroundColor Red "An error occurred: Failed to assign Fhir Data Contributor role to Fhir service instance"
        Exit
    }
 
    Write-Host('Generate postman environment file');
    # Create the environment structure
    $environment = @{
        id = ([System.Guid]::NewGuid()).ToString()
        name = "$WorkspaceName-$FHIRServiceName Environment"
        values = @(
            @{
                key = "clientId"
                value = $clientid
                enabled = $true
            },
            @{
                key = "clientSecret"
                value = $clientsecret
                enabled = $true
            },
        @{
                key = "fhirUrl"
                value = "https://$WorkspaceName-$FHIRServiceName.fhir.azurehealthcareapis.com"
                enabled = $true
            },
            @{
                key = "accessTokenUrl"
                value = "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token"
                enabled = $true
            }
        )
        timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        _postman_variable_scope = "environment"
        _postman_exported_at = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        _postman_exported_using = "Postman/10.0.0"
    }
   
    # Convert to JSON
    $json = $environment | ConvertTo-Json -Depth 10
   
    # Define the output file path
    $outputFile = "$WorkspaceName-$FHIRServiceName-environment.json"
   
    # Write the JSON to the file
    Set-Content -Path $outputFile -Value $json
   
    Write-Output "Postman environment JSON file has been created: $outputFile"
}
catch {
    # Error handling code - powershell catches the exception messages
    Write-Host -foregroundcolor Red "An error occurred: $_"
}