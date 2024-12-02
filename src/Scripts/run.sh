#!/bin/bash

kill_dotnet_processes() {
  pkill -f "dotnet"
}

cd ../DistributedFileSystem.WorkerNode || exit
dotnet clean
dotnet build
dotnet run &

sleep 1

kill_dotnet_processes

cd ../DistributedFileSystem.MasterNode || exit
dotnet clean
dotnet build
dotnet run &

sleep 1

kill_dotnet_processes

cd ../DistributedFileSystem.Client || exit
dotnet clean
dotnet build
dotnet run &

wait
