@echo off
echo Starting Distributed File System services...

:: Start Client
start "" ".\DFS\Client\DistributedFileSystem.Client.exe"

:: Start MasterNode
start "" ".\DFS\Master\DistributedFileSystem.MasterNode.exe"

:: Start WorkerNode
start "" ".\DFS\Worker\DistributedFileSystem.WorkerNode.exe"

echo All services started.
pause