<#
.SYNOPSIS
    Process the exported NDJson files in batches .
.DESCRIPTION
    This script will copy the exported NDJson files to container 'ndjson' which is linked to Bulk FHIR Loader in batches
.PARAMETER src
    The name of the source container http link.
.PARAMETER dest
    The name of the destination container http link.
.INPUTS
    None.
.OUTPUTS
    None.
.EXAMPLE
    .\CopyFiles.ps1 -src '<Source Container link>' -dest '<destination container link>' 
#>

[cmdletbinding()]
Param(
    [Parameter(Mandatory = $true)]
    [string]$src,
    [Parameter(Mandatory = $true)]
    [string]$dest
)

$Tags = @{
    "app-id" = "fhir-to-fhir-data-movement-sample"
}
$homelocation = Get-Location
$logPath = "$($homelocation)/logfiles"

## Check log directory exists , if not create new one
if (!(Test-Path -Path $logPath)) {
    $dir = New-Item -Path "$($homelocation)" -Name "logfiles" -ItemType "directory"
    if ($dir.Exists) {
        Write-Host "Log file directory created" -ForegroundColor Green
    }
}

$currentTime = Get-Date -Format FileDateTime
$Logfile = "$($logPath)/$($currentTime).log"

function New-CopyProcess {
    try {
        Write-Host "Start copying files from : $($src) to destination container : $($dest)" -ForegroundColor Green
        $azcpy = azcopy copy "$($src)" "$($dest)" --recursive

        ## Adding output to log file
        Add-content $Logfile -value $azcpy

        return $true
    }
    catch {
        Write-Host "Encounter error while copying files" -ForegroundColor Red
        throw
    }
      
}

## Copying files
if (New-CopyProcess) {
    Write-Host "Copy files completed. Please check log at $($Logfile) for more details on copy operation." -ForegroundColor Green
}