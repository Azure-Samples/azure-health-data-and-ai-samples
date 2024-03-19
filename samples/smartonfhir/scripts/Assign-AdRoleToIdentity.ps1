$clientId = "YOUR_CLIENT_ID" # Replace with your Microsoft Entra ID client ID
$tenantId = "YOUR_TENANT_ID" # Replace with your Microsoft Entra ID tenant ID
$objectId = "YOUR_OBJECT_ID" # Replace with the object ID of the managed identity
$roleName = "ROLE_NAME" # Replace with the name of the Microsoft Entra ID role you want to grant

# Construct the URL for the request
$url = "https://graph.microsoft.com/v1.0/roleManagement/$objectId/appRoleAssignments"

# Construct the JSON body for the request
$body = @{
    appRoleId = (Get-AzureADDirectoryRole -Filter "displayName eq '$roleName'").ObjectId
    principalId = $objectId
} | ConvertTo-Json

# Send the POST request
Invoke-RestMethod -Method Post -Uri $url -Headers @{
    "Authorization" = "Bearer $((Get-AzAccessToken -ResourceUrl "https://graph.microsoft.com").AccessToken)"
    "Content-Type" = "application/json"
} -Body $body