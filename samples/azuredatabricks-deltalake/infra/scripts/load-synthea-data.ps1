Set-Location $HOME
wget -O azcopy.tar.gz https://aka.ms/downloadazcopy-v10-linux && tar -xf azcopy.tar.gz --strip-components=1

./azcopy login --identity --identity-client-id "$env:MSI"
./azcopy copy https://ahdssampledata.blob.core.windows.net/fhir/synthea-ndjson-100/* https://mikaelwexpprem.blob.core.windows.net/import --s2s-preserve-access-tier=false

$Ctx = New-AzStorageContext -StorageAccountName $env:STORAGE_ACCOUNT_NAME -UseConnectedAccount
$FhirFiles = Get-AzStorageBlob -Container "import" -Blob "*.ndjson" -Context $Ctx

$importJsonObject = @{
    "resourceType" = "Parameters"
    "parameter" = New-Object System.Collections.Generic.List[object]
}

$importJsonObject.parameter.Add(@{
    "name" = "inputFormat"
    "valueString" = "application/fhir+ndjson"
})

$importJsonObject.parameter.Add(@{
    "name" = "mode"
    "valueString" = "IncrementalLoad"
})

# Loop over each FHIR file and add to JSON object
foreach ($file in $FhirFiles) {
    # Get the blob name without extension
    $resourceType = $file.Name -replace ".ndjson", ""

    $url = "https://$env:STORAGE_ACCOUNT_NAME.blob.core.windows.net/import/" + $file.Name

    $input = @{
        "name" = "input"
        "part" = @(
            @{
                "name" = "type"
                "valueString" = $resourceType
            },
            @{
                "name" = "url"
                "valueUri" = $url
            }
        )
    }
    
    $importJsonObject.parameter.Add($input)
}

# Loop over each FHIR file and add to JSON object
foreach ($file in $FhirFiles) {
    # Get the blob name without extension
    $resourceType = $file.Name -replace ".ndjson", ""

    $url = "https://$env:STORAGE_ACCOUNT_NAME.blob.core.windows.net/import/" + $file.Name

    $input = @{
        "name" = "input"
        "part" = @(
            @{
                "name" = "type"
                "valueString" = $resourceType
            },
            @{
                "name" = "url"
                "valueUri" = $url
            }
        )
    }
    
    $importJsonObject.parameter += $input
}

# Convert to JSON
# $importJson = $importJsonObject | ConvertTo-Json -Depth 4

# Get token for import
$importAccessToken = Get-AzAccessToken -ResourceUrl $env:FHIR_URL

Write-Host "Starting import to $env:FHIR_URL..." -ForegroundColor Yellow
$importHeaders = @{
    'Authorization' = "Bearer $($importAccessToken.token)"
    'Prefer' = "respond-async"
    'Content-Type' = "application/fhir+json"
}
$importRequestOutput = Invoke-RestMethod -Headers $importHeaders -Uri "$env:FHIR_URL/`$import" -Method POST -ContentType 'application/json' -Body $importJsonObject

# Print status code
Write-Host "Status code: " $importRequestOutput.StatusCode

# Print Content-Location header
Write-Host "Content-Location: " $importRequestOutput.Headers["Content-Location"]