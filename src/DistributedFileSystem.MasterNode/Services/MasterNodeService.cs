using DistributedFileSystem.WorkerNode;
using Grpc.Net.Client;
using Grpc.Core;
using DistributedFileSystem.MasterNode;
using DistributedFileSystem.MasterNode.Services;


public class MasterNodeService : MasterNode.MasterNodeBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MasterNodeService> _logger;
    public MasterNodeService(MongoDbService mongoDbService, ILogger<MasterNode> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = _logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<CreateNodeResponse> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        bool response = await _mongoDbService.CreateNode(request.workerAddress);
        if (response)
        {
            return new CreateNodeResponse
            {
                Status = true,
                Message = "Node created successfully."
            };
        }
        else
        {
            return new CreateNodeResponse
            {
                Status = false,
                Message = "Node failed to be createed."
            };
        }
    }

    public override async Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
    {
        bool response = await _mongoDbService.CreateNode(request.workerAddress);
        if (response)
        {
            return new DeleteNodeResponse
            {
                Status = true,
                Message = "Node deleted successfully."
            };
        }
        else
        {
            return new DeleteNodeResponse
            {
                Status = false,
                Message = "Node failed to be deleted."
            };
        }
    }

    public override async Task<HandleFilesResponse> HandleFiles(HandleFilesRequest request, ServerCallContext context)
    {
        var worker = _mongoDbService.GetOptimalWorker(request.ChunkSize).ToString();
        if (!string.IsNullOrEmpty(worker))
        {
            await _mongoDbService.UpdateWorkerStatus(worker.ToString(), "working");
            var channel = GrpcChannel.ForAddress(worker);
            var client = new WorkerNode.WorkerNodeClient(channel);
            var chunkId = Guid.NewGuid().ToString();

            var workerRequest = new StoreChunkRequest
            {
                ChunkId = chunkId,
                ChunkData = request.ChunkData
            };

            var workerResponse = await client.StoreChunkAsync(workerRequest);
            if (workerResponse.Status)
            {
                _logger.LogInformation("Chunk stored successfully at worker node.");
                return new HandleFilesResponse
                {
                    Status = true,
                    Message = workerResponse.Message
                };
                // GET RESOURCES AND UPDATE MONGODB
            }
            else
            {
                _logger.LogError("No optimal worker found");
                return new HandleFilesResponse
                {
                    Status = false,
                    Message = "Failed to find optimal worker to store your files."
                };
            }
        }
        else
        {
            _logger.LogError("No optimal worker found");
            return new HandleFilesResponse
            {
                Status = false,
                Message = "Failed to find optimal worker to store your files."
            };
        }
    }
    public override async Task<ChunkLocationsResponse> ChunkLocations(ChunkLocationsRequest request, ServerCallContext context)
    {
        var chunkLocations = await _mongoDbService.GetChunkLocations(request.ChunkId);
        var response = new ChunkLocationsResponse();
        response.WorkerAddress.AddRange(chunkLocations);
        return response;
    }
}