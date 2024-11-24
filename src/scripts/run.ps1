Set-Location -Path "../DistributedFileSystem.WorkerNode"
dotnet build
dotnet run

Set-Location -Path "../DistributedFileSystem.MasterNode"
dotnet build
dotnet run

Set-Location -Path "../DistributedFileSystem.Client"
dotnet build
dotnet run
