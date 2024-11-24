using Google.Protobuf;
using Grpc.Core;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// <sumary>
// All of the functions in this file serve as a translator for gRPC calls. When these functions are called with their required parameters,
// it will return a RPC responses.
// </summary>

namespace DistributedFileSystem.WorkerNode.Services
{
    public class WorkerNodeService : WorkerNode.WorkerNodeBase
    {
        private readonly string _chunkStorageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Chunks");
        private readonly DriveInfo _driveInfo;
        private readonly ILogger<WorkerNodeService> _logger;

        public WorkerNodeService(ILogger<WorkerNodeService> logger)
        {
            _driveInfo = new DriveInfo("/");
            _logger = logger;
        }

        // Stores chunks on the worker node given a unique chunk id 
        public override Task<StoreChunkResponse> StoreChunk(StoreChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to StoreChunk call");
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            Directory.CreateDirectory(_chunkStorageDirectory);
            File.WriteAllBytes(chunkFilePath, request.ChunkData.ToByteArray());

            return Task.FromResult(new StoreChunkResponse { Status = true, Message = $"Chunk {request.ChunkId} stored successfully at {chunkFilePath}." });
        }

        // Returns the chunk given the unique chunk id for a given file
        public override Task<GetChunkResponse> GetChunk(GetChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to GetChunk call");
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                var chunkData = File.ReadAllBytes(chunkFilePath);

                return Task.FromResult(new GetChunkResponse
                {
                    Status = true,
                    Message = $"Chunk {request.ChunkId} retrieved successfully.",
                    ChunkData = ByteString.CopyFrom(chunkData)
                });
            }
            else
            {
                _logger.LogError("Failed to find ChunkId");
                return Task.FromResult(new GetChunkResponse { Status = false, Message = $"Chunk {request.ChunkId} not found." });
            }
        }

        // Will remove a chunk from worker node given a unique chunk id
        public override Task<DeleteChunkResponse> DeleteChunk(DeleteChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to DeleteChunk call");
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                File.Delete(chunkFilePath);
                _logger.LogInformation("Successfully deleted chunk");
                return Task.FromResult(new DeleteChunkResponse { Status = true, Message = $"Chunk {request.ChunkId} deleted successfully." });
            }
            else
            {
                _logger.LogError("Failed to delete chunk");
                return Task.FromResult(new DeleteChunkResponse { Status = false, Message = $"Chunk {request.ChunkId} deletion failed." });
            }
        }

        // Will return the operating system resources like CPU, Memory, and Disk space
        public override Task<ResourceUsageResponse> ResourceUsage(ResourceUsageRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to ResourceUsage call");
            float cpuUsage;
            long memoryUsage;
            long diskSpace = _driveInfo.AvailableFreeSpace;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                cpuUsage = GetCpuUsageWindows();
                memoryUsage = GetMemoryUsageWindows();
            }
            else
            {
                cpuUsage = GetCpuUsageUnix();
                memoryUsage = GetMemoryUsageUnix();
            }

            return Task.FromResult(new ResourceUsageResponse
            {
                Status = true,
                Message = "Successfully retrieved worker resource information.",
                CpuUsage = cpuUsage,
                MemoryUsage = memoryUsage,
                DiskSpace = diskSpace
            });
        }

        // HELPER FUNCTIONS FOR WINDOWS PLATFORMS
        private float GetCpuUsageWindows()
        {
            var output = RunCommand("wmic cpu get loadpercentage");
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1 && float.TryParse(lines[1].Trim(), out var cpuUsage))
            {
                return cpuUsage;
            }

            return 0;
        }

        private long GetMemoryUsageWindows()
        {
            var output = RunCommand("wmic OS get FreePhysicalMemory");
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1 && long.TryParse(lines[1].Trim(), out var memoryUsage))
            {
                return memoryUsage * 1024;
            }

            return 0;
        }

        // HELPER FUNCTIONS FOR UNIX LIKE PLATFORMS
        private float GetCpuUsageUnix()
        {
            var output = RunCommand("top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'");
            return float.TryParse(output, out var cpuUsage) ? cpuUsage : 0;
        }

        private long GetMemoryUsageUnix()
        {
            var output = RunCommand("free | grep Mem | awk '{print $3 * 1024}'");
            return long.TryParse(output, out var memoryUsage) ? memoryUsage : 0;
        }

        // Runs commands given the worker nodes unique operating system
        private string RunCommand(string command)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                using (var reader = process.StandardOutput)
                {
                    return reader.ReadToEnd().Trim();
                }
            }
        }
    }
}
