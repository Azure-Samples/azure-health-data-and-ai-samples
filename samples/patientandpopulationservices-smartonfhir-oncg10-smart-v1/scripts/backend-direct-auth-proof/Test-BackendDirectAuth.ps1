[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$TokenEndpoint,

    [Parameter(Mandatory = $true)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$Scope,

    [Parameter(Mandatory = $false)]
    [string]$FhirAudience,

    [Parameter(Mandatory = $false)]
    [string]$ClientAssertion,

    [Parameter(Mandatory = $false)]
    [string]$ClientAssertionFile,

    [Parameter(Mandatory = $false)]
    [string]$FhirBaseUrl,

    [Parameter(Mandatory = $false)]
    [string]$FhirRelativePath = "Patient?_count=1",

    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory
)

function Resolve-Assertion {
    param(
        [string]$InlineAssertion,
        [string]$AssertionFile
    )

    if (-not [string]::IsNullOrWhiteSpace($InlineAssertion) -and -not [string]::IsNullOrWhiteSpace($AssertionFile)) {
        throw "Provide either ClientAssertion or ClientAssertionFile, not both."
    }

    if (-not [string]::IsNullOrWhiteSpace($InlineAssertion)) {
        return $InlineAssertion.Trim()
    }

    if (-not [string]::IsNullOrWhiteSpace($AssertionFile)) {
        if (-not (Test-Path -Path $AssertionFile -PathType Leaf)) {
            throw "Client assertion file not found: $AssertionFile"
        }

        return (Get-Content -Path $AssertionFile -Raw).Trim()
    }

    throw "ClientAssertion or ClientAssertionFile is required."
}

function Resolve-Scope {
    param(
        [string]$RequestedScope,
        [string]$Audience
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedScope)) {
        return $RequestedScope.Trim()
    }

    if ([string]::IsNullOrWhiteSpace($Audience)) {
        throw "Provide Scope or FhirAudience."
    }

    return "$($Audience.TrimEnd('/'))/.default"
}

function ConvertFrom-Base64Url {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputString
    )

    $padded = $InputString.Replace('-', '+').Replace('_', '/')

    switch ($padded.Length % 4) {
        2 { $padded += '==' }
        3 { $padded += '=' }
    }

    return [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($padded))
}

function ConvertFrom-JwtSegment {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Segment
    )

    return (ConvertFrom-Base64Url -InputString $Segment) | ConvertFrom-Json
}

function Get-JwtParts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Token
    )

    $segments = $Token.Split('.')
    if ($segments.Length -lt 2) {
        throw "Token is not a valid JWT."
    }

    return [PSCustomObject]@{
        Header  = ConvertFrom-JwtSegment -Segment $segments[0]
        Payload = ConvertFrom-JwtSegment -Segment $segments[1]
    }
}

function Join-UriParts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BaseUrl,

        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    return "$($BaseUrl.TrimEnd('/'))/$($RelativePath.TrimStart('/'))"
}

function Write-OptionalJsonFile {
    param(
        [string]$Directory,
        [string]$FileName,
        [object]$Content
    )

    if ([string]::IsNullOrWhiteSpace($Directory)) {
        return
    }

    if (-not (Test-Path -Path $Directory -PathType Container)) {
        New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    }

    $path = Join-Path -Path $Directory -ChildPath $FileName
    $Content | ConvertTo-Json -Depth 20 | Set-Content -Path $path
}

$resolvedAssertion = Resolve-Assertion -InlineAssertion $ClientAssertion -AssertionFile $ClientAssertionFile
$resolvedScope = Resolve-Scope -RequestedScope $Scope -Audience $FhirAudience

$tokenRequestBody = @{
    grant_type            = "client_credentials"
    client_id             = $ClientId
    scope                 = $resolvedScope
    client_assertion_type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
    client_assertion      = $resolvedAssertion
}

Write-Host "Requesting backend access token directly from $TokenEndpoint"
$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri $TokenEndpoint `
    -ContentType "application/x-www-form-urlencoded" `
    -Body $tokenRequestBody

if ([string]::IsNullOrWhiteSpace($tokenResponse.access_token)) {
    throw "Token response did not contain access_token."
}

$decodedAccessToken = Get-JwtParts -Token $tokenResponse.access_token
$claims = $decodedAccessToken.Payload

$claimSummary = [PSCustomObject]@{
    iss               = $claims.iss
    aud               = $claims.aud
    azp               = $claims.azp
    appid             = $claims.appid
    sub               = $claims.sub
    scp               = $claims.scp
    roles             = $claims.roles
    tid               = $claims.tid
    exp               = $claims.exp
    nbf               = $claims.nbf
    clientIdMatchHint = if ($claims.azp) { "azp" } elseif ($claims.appid) { "appid" } else { $null }
}

$claimWarnings = [System.Collections.Generic.List[string]]::new()
if ([string]::IsNullOrWhiteSpace($claims.aud)) {
    $claimWarnings.Add("Missing aud claim.")
}
if ([string]::IsNullOrWhiteSpace($claims.azp) -and [string]::IsNullOrWhiteSpace($claims.appid)) {
    $claimWarnings.Add("Missing azp/appid claim. FHIR smartIdentityProviders validation depends on one of these.")
}
if ([string]::IsNullOrWhiteSpace($claims.scp)) {
    $claimWarnings.Add("Missing scp claim. This is a known risk for app-only tokens and should be checked against FHIR and Inferno expectations.")
}
if ($claims.roles) {
    $claimWarnings.Add("roles claim is present. Confirm whether FHIR accepts this instead of scp for the direct backend scenario.")
}

$fhirResult = $null
if (-not [string]::IsNullOrWhiteSpace($FhirBaseUrl)) {
    $fhirTestUri = Join-UriParts -BaseUrl $FhirBaseUrl -RelativePath $FhirRelativePath
    Write-Host "Calling FHIR endpoint $fhirTestUri"

    try {
        $fhirResponse = Invoke-WebRequest `
            -Method Get `
            -Uri $fhirTestUri `
            -Headers @{
                Authorization = "Bearer $($tokenResponse.access_token)"
                Accept        = "application/fhir+json"
            }

        $fhirResult = [PSCustomObject]@{
            requestUri  = $fhirTestUri
            statusCode  = [int]$fhirResponse.StatusCode
            succeeded   = $true
            responseBody = $fhirResponse.Content
        }
    }
    catch {
        $response = $_.Exception.Response
        $statusCode = $null
        $responseBody = $null

        if ($response) {
            $statusCode = [int]$response.StatusCode
            if ($response.PSObject.Properties.Name -contains "Content" -and $response.Content) {
                $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
            elseif ($response.PSObject.Methods.Name -contains "GetResponseStream") {
                $stream = $response.GetResponseStream()
                if ($stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $responseBody = $reader.ReadToEnd()
                    $reader.Dispose()
                }
            }
        }

        $fhirResult = [PSCustomObject]@{
            requestUri   = $fhirTestUri
            statusCode   = $statusCode
            succeeded    = $false
            responseBody = $responseBody
        }
    }
}

$summary = [PSCustomObject]@{
    tokenEndpoint      = $TokenEndpoint
    clientId           = $ClientId
    requestedScope     = $resolvedScope
    tokenType          = $tokenResponse.token_type
    expiresIn          = $tokenResponse.expires_in
    claims             = $claimSummary
    claimWarnings      = $claimWarnings
    fhirValidation     = $fhirResult
}

Write-OptionalJsonFile -Directory $OutputDirectory -FileName "token-response.json" -Content $tokenResponse
Write-OptionalJsonFile -Directory $OutputDirectory -FileName "access-token-header.json" -Content $decodedAccessToken.Header
Write-OptionalJsonFile -Directory $OutputDirectory -FileName "access-token-claims.json" -Content $claims
Write-OptionalJsonFile -Directory $OutputDirectory -FileName "proof-summary.json" -Content $summary
if ($fhirResult) {
    Write-OptionalJsonFile -Directory $OutputDirectory -FileName "fhir-result.json" -Content $fhirResult
}

Write-Host ""
Write-Host "=== Direct backend auth proof summary ==="
$summary | ConvertTo-Json -Depth 10
