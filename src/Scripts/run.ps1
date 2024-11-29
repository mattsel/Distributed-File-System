cd ..\DistributedFileSystem.WorkerNode
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"

cd ..\DistributedFileSystem.MasterNode
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"

cd ..\DistributedFileSystem.Client
dotnet clean
dotnet build
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run"