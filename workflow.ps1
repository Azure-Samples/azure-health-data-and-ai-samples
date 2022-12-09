<#
.SYNOPSIS
    Deploys Azure Logic App workflows.
.DESCRIPTION
    Deploys the workflows by uploading ARM template files to the File Share.
.PARAMETER ResourceGroup
    The name of the resource group where the Storage account is located.
.PARAMETER StorageAccount
    The name of the Storage account where the File Share is located.
.INPUTS
    None.
.OUTPUTS
    None.
.EXAMPLE
    New-WorkflowDeployment -ResourceGroup "rg-orchestration-ts" -StorageAccount "stmyaccountnamelogicts"
#>
function New-WorkflowDeployment {
    Param(
    [Parameter(Mandatory = $true)]
    $ResourceGroup,
    [Parameter(Mandatory = $true)]
    $StorageAccount,
    [Parameter(Mandatory = $false)]
    [Switch]$Production
    )

    $ErrorActionPreference = "Stop"
    $WarningPreference = "Continue"

    # Set path of workflow files
    $localDir = (Get-Location).Path
    # Get folders/workflows to upload
    $directoryPath = "/site/wwwroot/"
    $folders = Get-ChildItem -Path $localDir -Directory -Recurse | Where-Object { $_.Name.StartsWith("wf-") }

    if ($null -eq $folders) {
        Write-Host "No workflows found" -ForegroundColor Yellow
        return
    }

    # Get the storage account context
    $ctx = (Get-AzStorageAccount -ResourceGroupName $ResourceGroup -Name $StorageAccount).Context

    # Get the file share
    $fs = (Get-AZStorageShare -Context $ctx).Name

    # Get current IP
    $ip = (Invoke-WebRequest -uri "http://ifconfig.me/ip").Content

    try {
        # Open firewall
        Add-AzStorageAccountNetworkRule -ResourceGroupName $ResourceGroup -Name $StorageAccount -IPAddressOrRange $ip | Out-Null

        Write-Host -ForegroundColor Green "Uploading files connection.json and host.json to file share.."    
        #$directoryPath = "/site/wwwroot/"
        $conn_folder = "connection"
        $conn_files = Get-ChildItem -Path workflows/$conn_folder -Recurse -File
        foreach($file in $conn_files)
        {
            $filePath = $directoryPath + $file.Name
            $fSrc = $file.FullName
            try {
                # Upload file
                Set-AzStorageFileContent -Context $ctx -ShareName $fs -Source $fSrc -Path $filePath -Force -ea Stop | Out-Null
            } catch {
                # Happens if file is locked, wait and try again
                Start-Sleep -Seconds 5
                Set-AzStorageFileContent -Context $ctx -ShareName $fs -Source $fSrc -Path $filePath -Force -ea Stop | Out-Null
            }
        }
        Write-Host 'Done uploading connection and host json file' -ForegroundColor Green

        Write-Host -ForegroundColor Green "Uploading workflow to file share.."    
        # Upload folders to file share
        foreach($folder in $folders)
        {
            Write-Host "Uploading workflow " -NoNewLine
            Write-Host $folder.Name -ForegroundColor Yellow -NoNewLine
            Write-Host "..." -NoNewLine
            $path = $directoryPath + $folder.Name
            Get-AzStorageShare -Context $ctx -Name $fs | New-AzStorageDirectory -Path $path -ErrorAction SilentlyContinue | Out-Null
            Start-Sleep -Seconds 1

            # Upload files to file share
            $files = Get-ChildItem -Path workflows/$folder -Recurse -File
            foreach($file in $files)
            {
                $filePath = $path + "/" + $file.Name
                $fSrc = $file.FullName
                try {
                    # Upload file
                    Set-AzStorageFileContent -Context $ctx -ShareName $fs -Source $fSrc -Path $filePath -Force -ea Stop | Out-Null
                } catch {
                    # Happens if file is locked, wait and try again
                    Start-Sleep -Seconds 5
                    Set-AzStorageFileContent -Context $ctx -ShareName $fs -Source $fSrc -Path $filePath -Force -ea Stop | Out-Null
                }
            }

            Write-Host 'Done' -ForegroundColor Green
        }
    } finally {
        # Remove the firewall rule
        Remove-AzStorageAccountNetworkRule -ResourceGroupName $ResourceGroup -Name $StorageAccount -IPAddressOrRange $ip | Out-Null
    }
}
 
New-WorkflowDeployment