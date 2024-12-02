param(
    [string]$Port
)
Set-Location "..\DistributedFileSystem.WorkerNode"
Start-Process "dotnet" -ArgumentList "run $Port"
Write-Host "Application started with Port: $Port and Host: $Host"