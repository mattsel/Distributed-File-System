using DistributedFileSystem.WorkerNode;
using Grpc.Core;
using Google.Protobuf;

public class WorkerNodeService : WorkerNode.WorkerNodeBase
{
    private readonly string _chunkStorageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Chunks");

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
}
