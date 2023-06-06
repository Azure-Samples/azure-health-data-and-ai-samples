Write-Host ""
Write-Host "Loading azd .env file from current environment..."
Write-Host ""

$output = azd env get-values

foreach ($line in $output) {
    $name, $value = $line.Split("=")
    $value = $value -replace '^\"|\"$'
    [Environment]::SetEnvironmentVariable($name, $value)
}

Write-Host "Environment variables set."

$NAME=$env:AZURE_ENV_NAME

Write-Host ""
Write-Host "Ensuring Microsoft Graph Powershell module is installed..."
Write-Host ""

#TODO - add a function to check this
$MicrosoftGraphPowershellSessionValid = True



Import-Module Microsoft.Graph.Identity.DirectoryManagement
$OrganizationInformation = Invoke-AzRestMethod 'https://graph.microsoft.com/v1.0/organization?$select=id,assignedPlans'
#TODO - error checking 
$PrimaryDomain = $($OrganizationInformation | Select-Object -ExpandProperty Content | ConvertFrom-Json | Select-Object -ExpandProperty value | Select-Object -ExpandProperty where { $_.OutputType } | Where-Object { $_.isDefault })[0].name


# Step 2 - Create FHIR Application Registration (if needed). Update if needed. Store values.

if ([string]::IsNullOrEmpty($env:AZD_FHIR_APP_REGISTRATION)) {
    
    Write-Host ""
    Write-Host 'Creating FHIR Service custom application registration...'
    Write-Host ""

    $AppJsonBodyFileLocation = Join-Path -Path $PSScriptRoot -ChildPath "fhir-app-manifest.json"
    $AppJsonBody = Get-Content -Raw -Path $AppJsonBodyFileLocation | ConvertFrom-Json
    $AppJsonBody.displayName = "${$env:AZURE_ENV_NAME}-fhir-app"
    $AppJsonBody.identifierUris = @("https://${$env:AZURE_ENV_NAME}-fhir-app.${$PrimaryDomain}")
    
    Invoke-AzRestMethod '
    Get-Location | Select-Object -ExpandProperty Path

    dotnet run --project "app/prepdocs/PrepareDocs/PrepareDocs.csproj" -- `
        './data/*.pdf' `
        --storageendpoint $env:AZURE_STORAGE_BLOB_ENDPOINT `
        --container $env:AZURE_STORAGE_CONTAINER `
        --searchendpoint $env:AZURE_SEARCH_SERVICE_ENDPOINT `
        --searchindex $env:AZURE_SEARCH_INDEX `
        --formrecognizerendpoint $env:AZURE_FORMRECOGNIZER_SERVICE_ENDPOINT `
        --tenantid $env:AZURE_TENANT_ID `
        -v

    azd env set AZD_PREPDOCS_RAN "true"
} else {
    Write-Host "AZD_PREPDOCS_RAN is set to true. Skipping the run."
}