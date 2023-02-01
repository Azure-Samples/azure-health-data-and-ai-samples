<#
.SYNOPSIS
    Process the exported NDJson files in batches .
.DESCRIPTION
    This script will copy the exported NDJson files to container 'ndjson' which is linked to Bulk FHIR Loader in batches
.PARAMETER srcResourceGroup
    The name of the resource group where the Source Storage account is located.
.PARAMETER srcStorageAccount
    The name of the Source Storage account where the exported NDJSON files are located.
.PARAMETER sourceContainer
    The name of the Source Container where the exported NDJSON files are located.
.PARAMETER destResourceGroup
    The name of the resource group where the Destination Storage account is located.
.PARAMETER destStorageAccount
    The name of the Destination Storage account where ndjson container is located.
.PARAMETER FileCount
    The number of files that need to be processed in single batch.
.PARAMETER BundleCount
    The number of bundle files count in container 'bundles' in destination storage account.
.INPUTS
    None.
.OUTPUTS
    None.
.EXAMPLE
    .\BatchProcess.ps1 -srcResourceGroup 'fhir2fhir' -srcStorageAccount 'fhirexportdata' -destResourceGroup 'fhirv1tofhirv1' -destStorageAccount 'bulk25886store' -sourceContainer 'export-141' -FileCount '30' -BundleCount '3000'
#>

[cmdletbinding()]
Param(
    [Parameter(Mandatory = $true)]
    [string]$srcResourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$srcStorageAccount,
    [Parameter(Mandatory = $true)]
    [string]$destResourceGroup,
    [Parameter(Mandatory = $true)]
    [string]$destStorageAccount,
    [Parameter(Mandatory = $true)]
    [string]$sourceContainer,
    [int]$FileCount = 30,
    [int]$BundleCount = 5000

)

$Tags = @{
    "app-id" = "fhir-to-fhir-data-movement-sample"
}

$bundlesCount = $BundleCount
$FilesCount = $FileCount
$srcContainer = $sourceContainer
$bundlescontainer = "bundles"
$destContainer = "ndjson"
$batchFileprocessedContainer = "batchprocessed-files"


function New-ContainerCreate {
    try {
        $containerExists = az storage container exists --name $batchFileprocessedContainer --account-name $destStorageAccount --auth-mode login
        if (($containerExists | ConvertFrom-Json).exists) {
            Write-Host "Container '$batchFileprocessedContainer' exists"
        }
        else {
            $containerCreate = az storage container create -n $batchFileprocessedContainer --account-name $destStorageAccount --auth-mode login
            if (($containerCreate | ConvertFrom-Json).created) {
                Write-Host "Container '$batchFileprocessedContainer' Created" -ForegroundColor Green
            }
            else{
                Write-Host "Error in creating Container '$batchFileprocessedContainer'" -ForegroundColor Red
            }
        }
    }
    catch {
        Write-Host "Error in checking Container exists Or Creation in Container"
        throw
    }
}

function New-BatchProcess {
    Param(
    [Parameter(Mandatory = $true)]
    $srcResourceGroup,
    [Parameter(Mandatory = $true)]
    $srcStorageAccount,
    [Parameter(Mandatory = $true)]
    $destResourceGroup,
    [Parameter(Mandatory = $true)]
    $destStorageAccount
    )

    do {
        try {
            # Get the source storage account context
            Write-Host "Getting Source Storage account Context"
            $srcCtx = (Get-AzStorageAccount -ResourceGroupName $srcResourceGroup -Name $srcStorageAccount).Context
            Write-Host "Getting the blobs from source storage container"
            $blobs = Get-AzStorageBlob -Container $srcContainer -Context $srcCtx

            # Get the destination storage account context
            Write-Host "Getting destination Storage account Context"
            $destCtx = (Get-AzStorageAccount -ResourceGroupName $destResourceGroup -Name $destStorageAccount).Context
            Write-Host "Getting the blobs from container bundles"
            $bundles = Get-AzStorageBlob -Container $bundlescontainer -Context $destCtx
            
            if($blobs.Count -ne 0 -And $bundles.length -lt $bundlesCount)
            {
                for ($i = 0; $i -lt $FilesCount; $i++) {

                    if($blobs[$i])
                    {
                        # Getting the file name from the exported directory
                        $filename=Split-Path -Path $blobs[$i].Name -Leaf

                        try {
                            # Copy file to ndjson container                
                            Write-Host "Copying the file: $($filename) to ndjson and batch container .."                
                            $destBlob = Copy-AzStorageBlob -SrcContainer $srcContainer -SrcBlob $($blobs[$i].Name) -DestContainer $destContainer -DestBlob $filename -Context $srcCtx -DestContext $destCtx -Force
                            
                            #Copy file to Batch container
                            $destBlobprocessed = Copy-AzStorageBlob -SrcContainer $srcContainer -SrcBlob $($blobs[$i].Name) -DestContainer $batchFileprocessedContainer -DestBlob $filename -Context $srcCtx -DestContext $destCtx -Force
                            
                            if($destBlob.BlobProperties.BlobCopyStatus -eq 'Success' -And $destBlobprocessed.BlobProperties.BlobCopyStatus -eq 'Success')
                            {
                                Write-Host "Copying of file: $($filename) to ndjson and Batch processed container is completed" -ForegroundColor Green
                                
                                try {                                        
                                    Write-Host "Removing the file: $($filename) from source container .."                        
                                    Remove-AzStorageBlob -Container $srcContainer -Blob $blobs[$i].Name -Context $srcCtx
                                    Write-Host "Removing of file: $($filename) from source container is completed" -ForegroundColor Green
                                }
                                catch {
                                    # Retrying the copy of file to batch processed folder
                                    Write-Host "Removing the file: $($blobs[$i].Name) from source container after 5 seconds" -ForegroundColor Yellow
                                    Start-Sleep -Seconds 5
                                    Remove-AzStorageBlob -Container $srcContainer -Blob $blobs[$i].Name -Context $srcCtx
                                    Write-Host "Moving of file: $($filename) to processed container is completed" -ForegroundColor Green
                                }
                            }
                        }
                        catch {
                            # Catching the error while copy file
                            Write-Host "Encounter error while copying the file: $($filename)" -ForegroundColor Red
                            throw
                        }
                    }
                                    
                }
                Write-Host "Next Batch will run in 5 Minutes" -ForegroundColor Yellow
                Start-Sleep -Seconds 300
            }
            else
            {
                Write-Host "Not processing NDJSON file in this run.." -ForegroundColor Yellow
                Write-Host "Next Batch will run in 5 Minutes" -ForegroundColor Yellow
                Start-Sleep -Seconds 300
            }
        }
        catch {
            Write-Host "Error running this batch" -ForegroundColor Red
            throw
        }
    }until ($blobs.Count -eq 0)
}
 
#Creating batch processed file container
New-ContainerCreate

#Process the files in batches
New-BatchProcess `
-srcResourceGroup $srcResourceGroup `
-srcStorageAccount $srcStorageAccount `
-destResourceGroup $destResourceGroup `
-destStorageAccount $destStorageAccount