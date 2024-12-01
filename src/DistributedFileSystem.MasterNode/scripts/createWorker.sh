#!/bin/bash

if [ -z "$1" ]; then
    echo "Using $0 <Port>"
    exit 1
fi

Port=$1

cd ../DistributedFileSystem.WorkerNode || { echo "Failed to find directory"; exit 1;}

dotnet run -- "$Port" &