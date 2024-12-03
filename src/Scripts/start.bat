@echo off
set SCRIPT_DIR=%~dp0

set CLIENT_EXEC=%SCRIPT_DIR%\Client\DistributedFileSystem.Client.exe
set MASTERNODE_EXEC=%SCRIPT_DIR%\Master\DistributedFileSystem.MasterNode.exe
set WORKERNODE_EXEC=%SCRIPT_DIR%\Worker\DistributedFileSystem.WorkerNode.exe

start "" "%MASTERNODE_EXEC%"

start "" "%WORKERNODE_EXEC%"

doskey DFS="%CLIENT_EXEC%"

echo All services started. You can now run the Client using the 'DFS' command.
pause