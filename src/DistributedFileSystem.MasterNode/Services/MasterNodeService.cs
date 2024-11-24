using DistributedFileSystem.MasterNode;
using Grpc.Core;
using Grpc.Net.Client;
using DistributedFileSystem.MasterNode.Services;
using DistributedFileSystem.WorkerNode;

public class MasterNodeService : MasterNode.MasterNodeBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MasterNodeService> _logger;

    public MasterNodeService(MongoDbService mongoDbService, ILogger<MasterNodeService> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<CreateNodeResponse> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        bool response = await _mongoDbService.CreateNode(request.WorkerAddress);
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
                Message = "Node failed to be created."
            };
        }
    }

    public override async Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
    {
        bool response = await _mongoDbService.DeleteNode(request.WorkerAddress);
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
        var worker = await _mongoDbService.GetOptimalWorker(request.ChunkSize);
        if (worker != null)
        {
            var channel = GrpcChannel.ForAddress(worker);
            var client = new WorkerNode.WorkerNodeClient(channel);
            var chunkId = Guid.NewGuid().ToString();
            await _mongoDbService.UpdateWorkerStatus(worker, "working", request.FileName, chunkId);

            var workerRequest = new StoreChunkRequest
            {
                ChunkId = chunkId,
                ChunkData = request.ChunkData
            };

            var workerResponse = await client.StoreChunkAsync(workerRequest);
            if (workerResponse.Status)
            {
                _logger.LogInformation("Chunk stored successfully at worker node.");

                var resourceRequest = new ResourceUsageRequest();
                var resourceResponse = await client.ResourceUsageAsync(resourceRequest);

                var updateMongo = await _mongoDbService.UpdateWorkerMetadataAsync(worker, "working", resourceResponse.CpuUsage, resourceResponse.MemoryUsage, resourceResponse.DiskSpace);

                if (updateMongo)
                {
                    return new HandleFilesResponse
                    {
                        Status = true,
                        Message = workerResponse.Message
                    };
                }
                else
                {
                    return new HandleFilesResponse
                    {
                        Status = false,
                        Message = "Failed to update MongoDB."
                    };
                }
            }
            else
            {
                _logger.LogError("Failed to store chunk at worker node.");
                return new HandleFilesResponse
                {
                    Status = false,
                    Message = "Failed to store chunk at worker node."
                };
            }
        }
        else
        {
            _logger.LogError("No optimal worker found.");
            return new HandleFilesResponse
            {
                Status = false,
                Message = "Failed to find optimal worker to store your files."
            };
        }
    }

    public override async Task<ChunkLocationsResponse> ChunkLocations(ChunkLocationsRequest request, ServerCallContext context)
    {
        var workerChunks = await _mongoDbService.GetWorkersByFileNameAsync(request.FileName);
        var response = new ChunkLocationsResponse();

        foreach (var worker in workerChunks)
        {
            response.WorkerAddress.Add(worker.Key);
            response.ChunkId.AddRange(worker.Value);
        }
        return response;
    }

    public override async Task<ListFilesResponse> ListFiles(ListFilesRequest request, ServerCallContext context)
    {
        var files = await _mongoDbService.GetAllFiles();
        var response = new ListFilesResponse();
        response.FileName.AddRange(files);
        return response;
    }

    public override async Task<GetWorkerResourcesResponse> GetWorkerResources(GetWorkerResourcesRequest request, ServerCallContext context)
    {
        var channel = GrpcChannel.ForAddress(request.WorkerAddress);
        var client = new WorkerNode.WorkerNodeClient(channel);
        var workerRequest = new ResourceUsageRequest();
        var workerResponse = await client.ResourceUsageAsync(workerRequest);

        return new GetWorkerResourcesResponse
        {
            Status = workerResponse.Status,
            Message = workerResponse.Message,
            CpuUsage = workerResponse.CpuUsage,
            MemoryUsage = workerResponse.MemoryUsage,
            DiskSpace = workerResponse.DiskSpace
        };
    }
}