#!/bin/bash

APP_DIR="$(pwd)"
if [ ! -d "$APP_DIR" ]; then
    echo "Error: $APP_DIR directory not found. Make sure the script is in the correct location."
    exit 1
fi

OS_TYPE=$(uname)
echo "Starting DistributedFileSystem.Client..."

if [[ "$OS_TYPE" == "Linux" || "$OS_TYPE" == "Darwin" ]]; then
    nohup "$APP_DIR/Client/DistributedFileSystem.Client" > "$APP_DIR/Client/client.log" 2>&1 &
    echo "DistributedFileSystem.Client started on Linux/Mac"
else
    nohup "$APP_DIR/Client/DistributedFileSystem.Client.exe" > "$APP_DIR/Client/client.log" 2>&1 &
    echo "DistributedFileSystem.Client started on Windows"
fi

echo "Starting DistributedFileSystem.MasterNode..."
nohup "$APP_DIR/Master/DistributedFileSystem.MasterNode" > "$APP_DIR/Master/masternode.log" 2>&1 &

echo "Starting DistributedFileSystem.WorkerNode..."
nohup "$APP_DIR/Worker/DistributedFileSystem.WorkerNode" > "$APP_DIR/Worker/workernode.log" 2>&1 &

if [[ "$OS_TYPE" == "Linux" || "$OS_TYPE" == "Darwin" ]]; then
    alias DFS="$APP_DIR/Client/DistributedFileSystem.Client"
else
    alias DFS="$APP_DIR/Client/DistributedFileSystem.Client.exe"
fi

echo "All services have been started."
echo "You can now use the DFS command for the Client application."
