
$scriptList = @(
    'change-rdp-port.ps1'
    'download-files.ps1'
);

foreach ($script in $scriptList) {
    # Start-Process -FilePath "$PSHOME\powershell.exe" -ArgumentList "-Command '& $script'" 
    # Write-Output "The $script is running."
    # Start-Sleep -Seconds 30
    $ScriptToRun= $PSScriptRoot+"\\"+$script
    & $ScriptToRun
}

Write-Output "Done"