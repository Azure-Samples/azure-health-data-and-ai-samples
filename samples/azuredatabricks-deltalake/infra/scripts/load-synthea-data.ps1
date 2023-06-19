Set-Location $HOME

$azCopyUrl = "https://aka.ms/downloadazcopy-v10-linux"
$azCopyFile = "azcopy.tar.gz"
Invoke-WebRequest $azCopyUrl -OutFile $azCopyFile
tar -xf $azCopyFile --strip-components=1

./azcopy login --identity --identity-resource-id "$env:MSI"
./azcopy copy "https://ahdssampledata.blob.core.windows.net/fhir/synthea-ndjson-1000/*" "https://$env:STORAGE_ACCOUNT_NAME.blob.core.windows.net/import" --s2s-preserve-access-tier=false

$Ctx = New-AzStorageContext -StorageAccountName $env:STORAGE_ACCOUNT_NAME -UseConnectedAccount
$FhirFiles = Get-AzStorageBlob -Container "import" -Context $Ctx
$ResourcesToLoad = "Patient", "Encounter", "Observation"

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
    $resourceType = $file.Name.Split(".")[0]

    if (-not $ResourcesToLoad -contains $resourceType)
    {
        Write-Host "Skipping resource type $resourceType because it's not used in this sample..."
        continue
    }

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

# Convert to JSON
$importJson = $importJsonObject | ConvertTo-Json -Depth 4

# Get token for import
$importAccessToken = Get-AzAccessToken -ResourceUrl $env:FHIR_URL

Write-Host "Starting import to $env:FHIR_URL..." -ForegroundColor Yellow
$importHeaders = @{
    'Authorization' = "Bearer $($importAccessToken.token)"
    'Prefer' = "respond-async"
}

try
{
    $importRequestOutput = Invoke-WebRequest -Headers $importHeaders -Uri "$env:FHIR_URL/`$import" -Method POST -ContentType 'application/fhir+json' -Body $importJson 
    # This will only execute if the Invoke-WebRequest is successful.
    $StatusCode = $importRequestOutput.StatusCode
    $ContentLocation = $importRequestOutput.Headers["Content-Location"][0]
} catch {
    Write-Error "Error in response: Status Code $($_.Exception.Response.StatusCode.value__)"
    Write-Error "Body: $($_.Exception.Response.Content)"
    Write-Error "Exception: $($_.Exception)"
    exit 1
}

# Print status code
Write-Host "Initial status code: $StatusCode"
Write-Host "Content-Location: " $ContentLocation

# Poll and wait for result
try {

    while ($StatusCode -eq 202) {
        # Wait for 30 seconds before the next poll
        Write-Host "Waiting for 30 seconds to check the content location uri..."
        Start-Sleep -Seconds 30

        $importAccessToken = Get-AzAccessToken -ResourceUrl $env:FHIR_URL
        $checkHeaders = @{
            'Authorization' = "Bearer $($importAccessToken.token)"
        }

        # Make a GET request to the polling URL
        Write-Host "Checking content location uri..."
        $pollingResponse = Invoke-WebRequest -Headers $checkHeaders -Uri $ContentLocation -Method GET 

        # Update the status code with the status code from the polling response
        $StatusCode = $pollingResponse.StatusCode
        Write-Host "Got status code $StatusCode"
    }

    # When the loop exits, the request has completed. Print the status code and response body.
    Write-Host "Final status code: $StatusCode"
    Write-Host "Final response: " $pollingResponse.Content
} catch {
    Write-Error "Error in polling response:"
    Write-Error $_.Exception
    $StatusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "Final status code: $StatusCode"
}
