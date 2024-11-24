using DistributedFileSystem.WorkerNode;
using Grpc.Core;
using Google.Protobuf;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DistributedFileSystem.WorkerNode.Services
{
    public class WorkerNodeService : WorkerNode.WorkerNodeBase
    {
        private readonly string _chunkStorageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Chunks");
        private readonly DriveInfo _driveInfo;
        private readonly ILogger<WorkerNodeService> _logger;

        public WorkerNodeService(ILogger<WorkerNodeService> logger)
        {
            _driveInfo = new DriveInfo(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C" : "/");
            _logger = logger;
        }

        public override Task<StoreChunkResponse> StoreChunk(StoreChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to StoreChunk call");
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            Directory.CreateDirectory(_chunkStorageDirectory);
            File.WriteAllBytes(chunkFilePath, request.ChunkData.ToByteArray());

            return Task.FromResult(new StoreChunkResponse { Status = true, Message = $"Chunk {request.ChunkId} stored successfully at {chunkFilePath}." });
        }

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

        private float GetCpuUsageWindows()
        {   
            using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
            { return cpuCounter.NextValue(); }
        }

        private long GetMemoryUsageWindows()
        {
            using (var ramCounter = new PerformanceCounter("Memory", "Available MBytes"))
            {
                return (long)ramCounter.NextValue() * 1024 * 1024;
            }
        }

        private float GetCpuUsageUnix()
        {
            var output = RunCommand("top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'");
            return float.TryParse(output, out var cpuUsage) ? cpuUsage : 0;
        }

        private long GetMemoryUsageUnix()
        {
            var output = RunCommand("free | grep Mem | awk '{print $3}'");
            return long.TryParse(output, out var memoryUsage) ? memoryUsage : 0;
        }

        private string RunCommand(string command)
        {
            var processInfo = new ProcessStartInfo("bash", $"-c \"{command}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                using (var reader = process.StandardOutput)
                { return reader.ReadToEnd().Trim(); }
            }
        }
    }
}
