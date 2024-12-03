#!/bin/bash

# Set the directory where the application was extracted
APP_DIR="./DFS"

# Start the Client service
echo "Starting DistributedFileSystem.Client..."
nohup "$APP_DIR/Client/DistributedFileSystem.Client" > "$APP_DIR/Client/client.log" 2>&1 &

# Start the MasterNode service
echo "Starting DistributedFileSystem.MasterNode..."
nohup "$APP_DIR/Master/DistributedFileSystem.MasterNode" > "$APP_DIR/Master/masternode.log" 2>&1 &

# Start the WorkerNode service
echo "Starting DistributedFileSystem.WorkerNode..."
nohup "$APP_DIR/Worker/DistributedFileSystem.WorkerNode" > "$APP_DIR/Worker/workernode.log" 2>&1 &

echo "All services have been started."
