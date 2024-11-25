cd ../DistributedFileSystem.WorkerNode
dotnet clean
dotnet build
dotnet run

cd ../DistributedFileSystem.MasterNode
dotnet clean
dotnet build
dotnet run

cd ../DistributedFileSystem.Client
dotnet clean
dotnet build
dotnet run