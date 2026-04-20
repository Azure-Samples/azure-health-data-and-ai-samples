param (
    [Parameter(Mandatory=$false)]
    [string]$ApiManagementHostName,
    
    [Parameter(Mandatory=$true)]
    [string]$AccessToken
)


if ([string]::IsNullOrWhiteSpace($ApiManagementHostName)) {

    Write-Host "ApiManagementHostName is not set."

    # Load parameters from active Azure Developer CLI environment
    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
            continue
        }
        
        if ([string]::IsNullOrWhiteSpace($ApiManagementHostName) -and $name -eq "ApiManagementHostName") {
            $ApiManagementHostName = $value.Trim('"')
        }
    }
}

# Send the POST request
$url = "https://" + $ApiManagementHostName + "/auth/block-access-token"

Write-Host "Revoking provided access token to url $url"

Invoke-RestMethod -Method Post -Uri $url -Headers @{
    "Content-Type" = "application/text"
} -Body $AccessToken