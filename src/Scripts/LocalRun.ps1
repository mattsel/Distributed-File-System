Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

cd ..\DistributedFileSystem.WorkerNode
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"

Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

cd ..\DistributedFileSystem.MasterNode
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"

Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force

cd ..\DistributedFileSystem.Client
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"
