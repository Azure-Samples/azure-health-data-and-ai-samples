param (
    [Parameter(Mandatory = $true)]
    [string]$AccessToken,

    [Parameter(Mandatory = $false)]
    [string]$RefreshToken,

    [Parameter(Mandatory = $false)]
    [string]$FunctionBaseUrl
)

# Microsoft Entra cannot natively revoke a (self-contained) access token. This gateway maintains a
# revocation blocklist: POST a token to /api/block-access-token and the gateway records its
# revocation key (access token uti/jti, or a hash for the opaque refresh token). Subsequent FHIR
# requests presenting the access token are rejected with 401, and refresh_token grants using the
# revoked refresh token are rejected with 400.

# Resolve the gateway base URL from the active azd environment when not supplied.
if ([string]::IsNullOrWhiteSpace($FunctionBaseUrl)) {

    Write-Host "FunctionBaseUrl is not set. Reading from azd environment..."

    # Resolve the sample root (parent of the scripts folder) — used by 'azd env get-values --cwd'.
    $SAMPLE_ROOT = Split-Path -Parent $PSScriptRoot

    $AZD_ENVIRONMENT = $(azd env get-values --cwd $SAMPLE_ROOT)
    $AZD_ENVIRONMENT | foreach {
        $name, $value = $_.split('=')
        if ([string]::IsNullOrWhiteSpace($name) -or $name.Contains('#')) {
            continue
        }

        if ([string]::IsNullOrWhiteSpace($FunctionBaseUrl) -and $name -eq "FunctionBaseUrl") {
            $FunctionBaseUrl = $value.Trim('"')
        }
    }
}

if ([string]::IsNullOrWhiteSpace($FunctionBaseUrl)) {
    Write-Error "FunctionBaseUrl not provided and not found in azd environment. Exiting."
    exit 1
}

# Send the POST request
$url = $FunctionBaseUrl.TrimEnd('/') + "/api/block-access-token"

function Revoke-Token([string]$label, [string]$token) {
    Write-Host "Revoking $label at $url"
    try {
        Invoke-RestMethod -Method Post -Uri $url -Headers @{ "Content-Type" = "text/plain" } -Body $token | Out-Null
        Write-Host "  $label revoked."
    }
    catch {
        Write-Error "  Failed to revoke $($label): $($_.Exception.Message)"
        exit 1
    }
}

Revoke-Token "access token" $AccessToken

if (-not [string]::IsNullOrWhiteSpace($RefreshToken)) {
    Revoke-Token "refresh token" $RefreshToken
}

Write-Host "Done. FHIR requests with the access token return 401; refresh with the refresh token returns 400."