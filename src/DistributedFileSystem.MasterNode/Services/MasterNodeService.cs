using DistributedFileSystem.MasterNode;
using Grpc.Core;
using Grpc.Net.Client;
using DistributedFileSystem.MasterNode.Services;
using DistributedFileSystem.WorkerNode;

// <summary>
// This class acts as the translator for gRPC calls to the master node. Many interactions with the master node will require the master node to interact with
// worker nodes to properly assist the client's needs. Much of it's memory is stored in MongoDb, to help with redundancy and reduce load on the systen
// </summary>

public class MasterNodeService : MasterNode.MasterNodeBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MasterNodeService> _logger;

    public MasterNodeService(MongoDbService mongoDbService, ILogger<MasterNodeService> logger)
    {
        _mongoDbService = mongoDbService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // When called will create a new worker node document in MongoDB to be used. Requires a https address to register a worker
    public override async Task<CreateNodeResponse> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to CreateNode call.");

        var channel = GrpcChannel.ForAddress(request.WorkerAddress);
        var client = new WorkerNode.WorkerNodeClient(channel);
        var resourceRequest = new ResourceUsageRequest();
        var resourceResponse = await client.ResourceUsageAsync(resourceRequest);

        if (resourceResponse.Status)
        {
            _logger.LogInformation($"Worker node resource usage: CPU - {resourceResponse.CpuUsage}%, Memory - {resourceResponse.MemoryUsage}%, Disk Space - {resourceResponse.DiskSpace} bytes");
        }
        else
        {
            _logger.LogError($"Failed to retrieve resource usage from the worker node at {request.WorkerAddress}");
            return new CreateNodeResponse { Status = false, Message = "Failed to retrieve worker resources." };
        }

        bool response = await _mongoDbService.CreateNode(request.WorkerAddress);

        if (response)
        {
            _logger.LogInformation("Successfully created node.");
            var updateResourceResult = await _mongoDbService.UpdateWorkerMetadataAsync(
                request.WorkerAddress,
                "waiting",
                resourceResponse.CpuUsage,
                resourceResponse.MemoryUsage,
                resourceResponse.DiskSpace);

            if (updateResourceResult)
            {
                _logger.LogInformation("Successfully updated worker metadata with resource usage.");
                return new CreateNodeResponse { Status = true, Message = "Node created successfully with resources." };
            }
            else
            {
                _logger.LogError("Failed to update worker metadata in MongoDB.");
                return new CreateNodeResponse { Status = false, Message = "Failed to update worker metadata in MongoDB." };
            }
        }
        else
        {
            _logger.LogError("Failed to create node.");
            return new CreateNodeResponse { Status = false, Message = "Node creation failed." };
        }
    }

    // When called it will delete the worker node from the database so it is no longer an active worker. Requries a https string to remove a node
    public override async Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to DeleteNode call");
        bool response = await _mongoDbService.DeleteNode(request.WorkerAddress);
        if (response)
        {
            _logger.LogInformation("Successfully deleted node");
            return new DeleteNodeResponse { Status = true, Message = "Node deleted successfully." };
        }
        else
        {
            _logger.LogError("Failed to delete node");
            return new DeleteNodeResponse { Status = false, Message = "Node failed to be deleted." };
        }
    }

    // When called will store files using a worker node. This function will use node balancing techniques to ensure minimal latency.
    public override async Task<HandleFilesResponse> HandleFiles(HandleFilesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to HandleFiles call");
        var worker = await _mongoDbService.GetOptimalWorker(request.ChunkSize);
        if (worker != null)
        {
            var channel = GrpcChannel.ForAddress(worker);
            var client = new WorkerNode.WorkerNodeClient(channel);
            var chunkId = Guid.NewGuid().ToString();
            await _mongoDbService.UpdateWorkerStatus(worker, "working", request.FileName, chunkId);

            var workerRequest = new StoreChunkRequest { ChunkId = chunkId, ChunkData = request.ChunkData };

            var workerResponse = await client.StoreChunkAsync(workerRequest);
            if (workerResponse.Status)
            {
                _logger.LogInformation("Chunk stored successfully at worker node.");

                var resourceRequest = new ResourceUsageRequest();
                var resourceResponse = await client.ResourceUsageAsync(resourceRequest);

                var updateMongo = await _mongoDbService.UpdateWorkerMetadataAsync(worker, "waiting", resourceResponse.CpuUsage, resourceResponse.MemoryUsage, resourceResponse.DiskSpace);

                if (updateMongo)
                {
                    _logger.LogInformation("Successfully stored file chunks");
                    return new HandleFilesResponse { Status = true, Message = workerResponse.Message };
                }
                else
                {
                    _logger.LogError("Failed to update MongoDB with worker's new metadata");
                    return new HandleFilesResponse { Status = false, Message = "Failed to update MongoDB." };
                }
            }
            else
            {
                _logger.LogError("Failed to store chunk at worker node.");
                return new HandleFilesResponse { Status = false, Message = "Failed to store chunk at worker node." };
            }
        }
        else
        {
            _logger.LogError("No optimal worker found.");
            return new HandleFilesResponse { Status = false, Message = "Failed to find optimal worker to store your files." };
        }
    }

    // When this function is called given a filename, it will return the location addresses of all chunks this file is stored to
    public override async Task<ChunkLocationsResponse> ChunkLocations(ChunkLocationsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to ChunkLocations call");
        var workerChunks = await _mongoDbService.GetWorkersByFileNameAsync(request.FileName);
        var response = new ChunkLocationsResponse();

        foreach (var worker in workerChunks)
        {
            response.WorkerAddress.Add(worker.Key);
            response.ChunkId.AddRange(worker.Value);
        }
        return response;
    }

    // Lists all the known filenames in the network
    public override async Task<ListFilesResponse> ListFiles(ListFilesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to ListFiles call");
        var files = await _mongoDbService.GetAllFiles();
        var response = new ListFilesResponse();
        response.FileName.AddRange(files);
        return response;
    }

    // This function will interact with a given worker node's address to get it's current resource amounts
    public override async Task<GetWorkerResourcesResponse> GetWorkerResources(GetWorkerResourcesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to GetWorkerResources call");
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