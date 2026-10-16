# Derive a public JWKS from an ES384 EC private key PEM.
# Emits keys[0] with kty=EC, crv=P-384, use=sig, alg=ES384, and an RFC 7638 thumbprint kid.
param(
    [Parameter(Mandatory = $true)] [string] $PrivateKeyPem,
    [Parameter(Mandatory = $true)] [string] $OutputPath
)

$ErrorActionPreference = 'Stop'

$pem = Get-Content -Path $PrivateKeyPem -Raw
$ec  = [System.Security.Cryptography.ECDsa]::Create()
$ec.ImportFromPem($pem)

$publicOnly = $ec.ExportParameters($false)
if ($publicOnly.Curve.Oid.FriendlyName -notin @('nistP384', 'ECDSA_P384') -and
    $publicOnly.Curve.Oid.Value -ne '1.3.132.0.34') {
    throw "Expected P-384 curve, got $($publicOnly.Curve.Oid.FriendlyName) / $($publicOnly.Curve.Oid.Value)"
}

function ConvertTo-Base64Url([byte[]] $bytes) {
    [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+','-').Replace('/','_')
}

$x = ConvertTo-Base64Url $publicOnly.Q.X
$y = ConvertTo-Base64Url $publicOnly.Q.Y

$canonical = '{"crv":"P-384","kty":"EC","x":"' + $x + '","y":"' + $y + '"}'
$sha       = [System.Security.Cryptography.SHA256]::Create()
$kid       = ConvertTo-Base64Url $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonical))

$jwks = [ordered]@{
    keys = @(
        [ordered]@{
            kty = 'EC'
            crv = 'P-384'
            use = 'sig'
            alg = 'ES384'
            kid = $kid
            x   = $x
            y   = $y
        }
    )
}

$json = $jwks | ConvertTo-Json -Depth 5
Set-Content -Path $OutputPath -Value $json -Encoding utf8
Write-Host "Wrote JWKS to $OutputPath"
Write-Host "kid = $kid"
