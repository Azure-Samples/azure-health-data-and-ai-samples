Param(
    [Parameter(Mandatory = $true, HelpMessage="Azure API for FHIR URL")]
    [string]$srcUrl,

    [Parameter(Mandatory = $true, HelpMessage="FHIR service URL")]
    [string]$destUrl
)

function GetSearchParameter {
    Param (
        [Parameter(Mandatory=$true)]
        [string]$Uri,
        
        [Parameter(Mandatory=$true)]
        [string]$accessToken
    )
        try {
            Write-Host "Fetching SearchParameters from Source FHIR Service..." -ForegroundColor Yellow
            $searchParameters = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken"} -Uri $Uri -Method Get -ContentType 'application/json' -MaximumRetryCount 3  -RetryIntervalSec 10
            if ($searchParameters.StatusCode -eq 200) {
                Write-Host "SearchParameters fetched successfully." -ForegroundColor Green
                return $searchParameters
            } 
        }
        catch {
            Write-Host "GetSearchParameters Exception: $_" -ForegroundColor Red
            throw
        }
}

function TransformObject {
    param (       
        [Parameter(Mandatory=$true)]
        [object]$searchParameters
    )
    try {
        # Iterate over each entry
        foreach ($entry in $searchParameters.entry) {
            # Remove the 'fullUrl' property from each resource 
            if ($entry.fullUrl) {
                $entry.PSObject.Properties.Remove("fullUrl")
            }

            # Create the 'requestObject' with 'url' and 'method' properties
            $requestObject = [PSCustomObject]@{
                url = "SearchParameter/$($entry.resource.id)"
                method = "PUT"
            }

            # Check if 'request' property exists in the resource object
            if (-not $entry.request) {
                # Add the 'requestObject' to the entry
                $entry | Add-Member -MemberType NoteProperty -Name request -Value $requestObject
            } else {
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
        [Parameter(Mandatory=$true)]
        [string]$Uri,
        
        [Parameter(Mandatory=$true)]
        [string]$accessToken,
        
        [Parameter(Mandatory=$true)]
        [string]$searchParameters
    )

        try {       
            $response = Invoke-WebRequest -Headers @{Authorization = "Bearer $accessToken"} -Uri $Uri -Method Post -ContentType 'application/json' -Body $searchParameters -MaximumRetryCount 3 -RetryIntervalSec 10
            if ($response.StatusCode -eq 200) {
                Write-Host "Search parameters posted successfully." -ForegroundColor Green
                return
            } else {
                Write-Host "PostSearchParameters Error: $($response.StatusCode) - $($response.StatusDescription)" -ForegroundColor Red
            }
        }
        catch {
            Write-Host "PostSearchParameters Exception: $_" -ForegroundColor Red
            throw
        }
}

try {

    $Azmodule_check = Get-Command az -ErrorVariable Azmodule_check -ErrorAction SilentlyContinue
    if (!$Azmodule_check) {
        Write-Host "Az CLI is not installed. Please install the az cli and re-run the script." -ForegroundColor Red
        Exit
    }

    $User_Check = az account show 2>&1
    if (!$?) {
        Write-Host "User not logged into the az account. Please login using az login." -ForegroundColor Red
        Exit
    }

    $src_url = [uri]$srcUrl
    $src_host_name = $src_url.Host

    $dest_url = [uri]$destUrl
    $dest_host_name = $dest_url.Host

    # Get access tokens for source and destination FHIR services
    $src_Access_token = az account get-access-token --scope "https://$src_host_name/.default"
    $src_token = $src_Access_token[1].TrimEnd(",","`"").Split(":")[1].TrimStart(" ","`"") 

    $dest_Access_token = az account get-access-token --scope "https://$dest_host_name/.default"
    $dest_token = $dest_Access_token[1].TrimEnd(",","`"").Split(":")[1].TrimStart(" ","`"")

    # Get search parameters from the source FHIR service
    $searchParameters = GetSearchParameter -Uri "https://$src_host_name/SearchParameter" -accessToken $src_token
    $searchparameterObject = $searchParameters | ConvertFrom-Json

    if($searchparameterObject.entry){
    $transformedObject = TransformObject -searchParameters $searchparameterObject
    # Post SearchParameters to destination FHIR service
    Write-Host "Posting SearchParameters to Destination FHIR Service..." -ForegroundColor Yellow
    PostSearchParameters -Uri "https://$dest_host_name" -accessToken $dest_token -searchParameters $transformedObject
    }
    else
    {
        Write-Host "SearchParameter is not present on the source FHIR server."
        Exit
    }
}
catch {
    Write-Host "An error occurred:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
}