Param(
    [Parameter(Mandatory = $true, HelpMessage = "Azure API for FHIR URL")]
    [string]$srcUrl,

    [Parameter(Mandatory = $true, HelpMessage = "FHIR service URL")]
    [string]$destUrl
)
function GetSearchParameter {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter(Mandatory = $true)]
        [string]$accessToken
    )
    try {
        Write-Host "Fetching SearchParameters from Source FHIR Service..." -ForegroundColor Yellow

        & {
            $ProgressPreference = 'SilentlyContinue'
            $searchParameters = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken" } -Uri $Uri -Method Get -ContentType 'application/json' -MaximumRetryCount 3  -RetryIntervalSec 10
        
            if ($searchParameters.StatusCode -eq 200) {
                $jsonString = [System.Text.Encoding]::UTF8.GetString($searchParameters.Content)
                $bundle = $jsonString | ConvertFrom-Json -Depth 100

                # Filter entries to only include custom search parameters (exclude HL7)
                $customEntries = $bundle.entry | Where-Object {
                    $_.resource.url -and ($_.resource.url -notlike "http://hl7.org/*")
                }

                if ($customEntries) {
                    # Reconstruct the bundle with only custom entries
                    $bundle.entry = $customEntries
                    Write-Host "Custom SearchParameters found: $($customEntries.Count)" -ForegroundColor Green
                    return $bundle
                }
                else {
                    Write-Host "No custom SearchParameters found." -ForegroundColor Yellow
                    return $null
                }
            } 
        }
    }
    catch {
        Write-Host "GetSearchParameters Exception: $_" -ForegroundColor Red
        throw
    }
}
function TransformObject {
    param (       
        [Parameter(Mandatory = $true)]
        [object]$searchParameters
    )
    try {
        # Iterate over each entry
        foreach ($entry in $searchParameters.entry) {
            # Remove the 'fullUrl' property from each resource 
            if ($entry.fullUrl) {
                $entry.PSObject.Properties.Remove("fullUrl")
            }

            # Remove the 'search' property from each resource 
            if ($entry.search) {
                $entry.PSObject.Properties.Remove("search")
            }

            # Create the 'requestObject' with 'url' and 'method' properties
            $requestObject = [PSCustomObject]@{
                url    = "SearchParameter/$($entry.resource.id)"
                method = "PUT"
            }

            # Check if 'request' property exists in the resource object
            if (-not $entry.request) {
                # Add the 'requestObject' to the entry
                $entry | Add-Member -MemberType NoteProperty -Name request -Value $requestObject
            }
            else {
                # If 'request' property already exists, update it
                $entry.request = $requestObject
            }
        }

        # Remove 'meta' and 'link' properties
        $searchParameters.PSObject.Properties.Remove("meta")
        $searchParameters.PSObject.Properties.Remove("link")
        # Update the 'type' property
        $searchParameters.type = "batch"

        # Convert the modified object back to JSON
        return $searchParameters | ConvertTo-Json -Depth 100
    }
    catch {
        throw $_
    }
}
function PostSearchParameters {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter(Mandatory = $true)]
        [string]$accessToken,
        
        [Parameter(Mandatory = $true)]
        [string]$searchParameters
    )

    try {       
        $response = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken" } -Uri $Uri -Method Post -ContentType 'application/json' -Body $searchParameters -MaximumRetryCount 3 -RetryIntervalSec 10
        if ($response.StatusCode -eq 200) {
            Write-Host "Search parameters posted successfully." -ForegroundColor Green
            return
        }
        else {
            Write-Host "PostSearchParameters Error: $($response.StatusCode) - $($response.StatusDescription)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "PostSearchParameters Exception: $_" -ForegroundColor Red
        throw
    }
}
function Start-Reindex {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        
        [Parameter(Mandatory = $true)]
        [string]$accessToken
    )

    try {
        Write-Host "Starting reindex operation..." -ForegroundColor Yellow
        
        $reindexBody = @{
            resourceType = "Parameters"
            parameter    = @()
        } | ConvertTo-Json -Depth 10

        $response = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken" } -Uri "$Uri/`$reindex" -Method Post -ContentType 'application/json' -Body $reindexBody -MaximumRetryCount 3 -RetryIntervalSec 10
        
        if ($response.StatusCode -eq 201 -or $response.StatusCode -eq 200) {
            Write-Host "Reindex operation initiated successfully." -ForegroundColor Green
            
            # Extract the location header which contains the status URL
            $locationHeader = $response.Headers.'content-location'
            if ($locationHeader) {
                return $locationHeader
            }
            else {
                Write-Host "Warning: No location header found in reindex response." -ForegroundColor Yellow
                return $null
            }
        }
    }
    catch {
        Write-Host "Start-Reindex Exception: $_" -ForegroundColor Red
        throw
    }
}
function Get-ReindexStatus {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$StatusUri,
        
        [Parameter(Mandatory = $true)]
        [string]$accessToken
    )

    try {
        $response = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken" } -Uri $StatusUri -Method Get -ContentType 'application/json' -MaximumRetryCount 3 -RetryIntervalSec 10
        
        if ($response.StatusCode -eq 200) {
            $jsonString = [System.Text.Encoding]::UTF8.GetString($response.Content)
            $statusObject = $jsonString | ConvertFrom-Json
            return $statusObject
        }
    }
    catch {
        Write-Host "Get-ReindexStatus Exception: $_" -ForegroundColor Red
        throw
    }
}
function Wait-ReindexCompletion {
    Param (
        [Parameter(Mandatory = $true)]
        [string]$StatusUri,
        
        [Parameter(Mandatory = $true)]
        [string]$accessToken,
        
        [Parameter(Mandatory = $false)]
        [int]$PollIntervalSeconds = 10,
        
        [Parameter(Mandatory = $false)]
        [int]$MaxWaitMinutes = 15
    )

    try {
        Write-Host "Polling reindex status at $StatusUri" -ForegroundColor Yellow
        Write-Host "Maximum wait time: $MaxWaitMinutes minutes" -ForegroundColor Yellow
        
        $completed = $false
        $attempts = 0
        $startTime = Get-Date
        
        while (-not $completed) {
            $attempts++
            
            # Check if maximum wait time exceeded
            $elapsedMinutes = ((Get-Date) - $startTime).TotalMinutes
            if ($elapsedMinutes -ge $MaxWaitMinutes) {
                Write-Host "`nReindex operation timeout reached after $MaxWaitMinutes minutes." -ForegroundColor Yellow
                Write-Host "The reindex operation is still running but has exceeded the script wait time." -ForegroundColor Yellow
                Write-Host "`nTo check the status manually, use the following URL:" -ForegroundColor Cyan
                Write-Host $StatusUri -ForegroundColor White
                Write-Host "`nYou can monitor the reindex status by making a GET request to the above URL." -ForegroundColor Cyan
                Write-Host "The script will now exit. Please check the reindex status manually." -ForegroundColor Yellow
                return 
            }
            
            Start-Sleep -Seconds $PollIntervalSeconds
            
            $status = Get-ReindexStatus -StatusUri $StatusUri -accessToken $accessToken            

            # Check for status in the Parameters resource            
            $currentStatus = ($status.parameter | Where-Object { $_.name -eq "status" }).valueString
            
            $remainingMinutes = [math]::Round($MaxWaitMinutes - $elapsedMinutes, 1)
            Write-Host "Reindex status (attempt $attempts, $remainingMinutes min remaining): $currentStatus" -ForegroundColor Cyan
            
            if ($currentStatus -eq "completed") {
                Write-Host "Reindex operation completed successfully!" -ForegroundColor Green                
                
                $completed = $true
            }
            elseif ($currentStatus -eq "failed") {
                Write-Host "Reindex operation failed." -ForegroundColor Red
                throw "Reindex operation failed."
            }
            elseif ($currentStatus -eq "cancelled") {
                Write-Host "Reindex operation was cancelled." -ForegroundColor Red
                throw "Reindex operation was cancelled."
            }
            # Continue polling for "running" or "queued" status
        }
    }
    catch {
        Write-Host "Wait-ReindexCompletion Exception: $_" -ForegroundColor Red
        throw
    }
}


try {

    $Azmodule_check = Get-Command az -ErrorAction SilentlyContinue
    if (-not $Azmodule_check) {
        Write-Host "Az CLI is not installed. Please install the az cli and re-run the script." -ForegroundColor Red
        Exit
    }

    az account show > $null 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "User not logged into the az account. Please login using az login." -ForegroundColor Red
        Exit
    }

    $src_url = [uri]$srcUrl
    $src_host_name = $src_url.Host

    $dest_url = [uri]$destUrl
    $dest_host_name = $dest_url.Host

    # Get access tokens for source and destination FHIR services    
    $src_token = az account get-access-token --scope "https://$src_host_name/.default" --query accessToken -o tsv 
        
    $dest_token = az account get-access-token --scope "https://$dest_host_name/.default" --query accessToken -o tsv

    # Get search parameters from the source FHIR service
    $searchParameters = GetSearchParameter -Uri "https://$src_host_name/SearchParameter?_count=1000" -accessToken $src_token    
    
    if ($searchParameters.entry) {
        $transformedObject = TransformObject -searchParameters $searchParameters
        
        # Post SearchParameters to destination FHIR service
        Write-Host "Posting SearchParameters to Destination FHIR Service..." -ForegroundColor Yellow
        PostSearchParameters -Uri "https://$dest_host_name" -accessToken $dest_token -searchParameters $transformedObject

        # Start reindex operation
        $reindexStatusUrl = Start-Reindex -Uri "https://$dest_host_name" -accessToken $dest_token
        
        if ($reindexStatusUrl) {
            # Poll for reindex completion
            Wait-ReindexCompletion -StatusUri $reindexStatusUrl -accessToken $dest_token -PollIntervalSeconds 10
        }
        else {
            Write-Host "Unable to track reindex status. Please check the FHIR service manually." -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "SearchParameter is not present on the source FHIR server."
        Exit
    }
}
catch {
    Write-Host "An error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}