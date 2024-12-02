@echo off
echo Stopping Distributed File System services...

:: Stop Client
taskkill /IM "DistributedFileSystem.Client.exe" /F

:: Stop MasterNode
taskkill /IM "DistributedFileSystem.MasterNode.exe" /F

:: Stop WorkerNode
taskkill /IM "DistributedFileSystem.WorkerNode.exe" /F

echo All services stopped.
pause