using DistributedFileSystem.WorkerNode;
using Grpc.Core;
using Google.Protobuf;

namespace DistributedFileSystem.WorkerNode.Services
{
    public class WorkerNodeService : WorkerNode.WorkerNodeBase
    {
        private readonly string _chunkStorageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Chunks");
        private readonly PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private readonly PerformanceCounter _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        private readonly DriveInfo _driveInfo = new DriveInfo("C");

        public override Task<StoreChunkResponse> StoreChunk(StoreChunkRequest request, ServerCallContext context)
        {
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            File.WriteAllBytes(chunkFilePath, request.ChunkData.ToByteArray());

            return Task.FromResult(new StoreChunkResponse
            {
                Status = true,
                Message = $"Chunk {request.ChunkId} stored successfully at {chunkFilePath}."
            });
        }

        public override Task<GetChunkResponse> GetChunk(GetChunkRequest request, ServerCallContext context)
        {
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                var chunkData = File.ReadAllBytes(chunkFilePath);

                return Task.FromResult(new GetChunkResponse
                {
                    Status = true,
                    Message = $"Chunk {request.ChunkId} retrieved successfully.",
                    ChunkData = Google.Protobuf.ByteString.CopyFrom(chunkData)
                });
            }
            else
            {
                return Task.FromResult(new GetChunkResponse
                {
                    Status = false,
                    Message = $"Chunk {request.ChunkId} not found."
                });
            }
        }
        public override Task<DeleteChunkResponse> DeleteChunk(DeleteChunkRequest request, ServerCallContext context)
        {
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                File.Delete(chunkFilePath);

                return Task.FromResult(new DeleteChunkResponse
                {
                    Status = true,
                    Message = $"Chunk {request.ChunkId} deleted successfully.",
                });
            }
            else
            {
                return Task.FromResult(new DeleteChunkResponse
                {
                    Status = false,
                    Message = $"Chunk {request.ChunkId} deleted failed.",
                });
            }
        }
        public override Task<ResourceUsageResponse> ResourceUsage(ResourceUsageRequest request, ServerCallContext context)
        {
            var cpuUsage = _cpuCounter.NextValue();
            var memoryUsage = _ramCounter.NextValue() * 1024 * 1024;
            var diskSpace = _driveInfo.AvailableFreeSpace;

            if (cpuUsage != null && memoryUsage != null && diskSpace != null)
            {
                return Task.FromResult(new ResourceUsageResponse
                {
                    Status = true,
                    Message = "Successfully retrieved worker resource information.",
                    CpuUsage = cpuUsage,
                    MemoryUsage = memoryUsage,
                    DiskSpace = diskSpace
                });
            }
            else
            {
                return Task.FromResult(new ResourceUsageResponse
                {
                    Status = false,
                    Message = "Failed to retrieved worker resource information.",
                    CpuUsage = "0",
                    MemoryUsage = "0",
                    DiskSpace = "0"
                });
            }
        }
    }
}
