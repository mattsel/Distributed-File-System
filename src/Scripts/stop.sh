#!/bin/bash

APP_DIR="./DFS"

echo "Stopping DistributedFileSystem.Client..."
pkill -f "DistributedFileSystem.Client"

echo "Stopping DistributedFileSystem.MasterNode..."
pkill -f "DistributedFileSystem.MasterNode"

echo "Stopping DistributedFileSystem.WorkerNode..."
pkill -f "DistributedFileSystem.WorkerNode"

echo "All services have been stopped."
