param (
    [Parameter(Mandatory=$true)]
    [string]$ApplicationId,

    [Parameter(Mandatory=$true)]
    [string]$UserObjectId,

    [Parameter(Mandatory=$true)]
    [string]$FhirUserValue
)

$appIdFormatted = $ApplicationId.Replace("-", "")

$token = $(az account get-access-token --resource-type ms-graph --query accessToken --output tsv)

$body = "{
    `"extension_$($appIdFormatted)_fhirUser`": `"$FhirUserValue`"
}"

Invoke-RestMethod -Uri "https://graph.microsoft.com/v1.0/users/$UserObjectId" -Headers @{Authorization = "Bearer $token"} -Method Patch -Body $body -ContentType application/json

Write-Host "Done"