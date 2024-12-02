using DistributedFileSystem.MasterNode;
using Grpc.Core;
using Grpc.Net.Client;
using Google.Protobuf;
using DistributedFileSystem.WorkerNode;
using System.Diagnostics;
using DistributedFileSystem.MasterNode.Models;
using System.Runtime.InteropServices;
using DistributedFileSystem.MasterNode.Helpers;

// <summary>
// This class acts as the translator for gRPC calls to the master node. Many interactions with the master node will require the master node to interact with
// worker nodes to properly assist the client's needs. Much of it's memory is stored in MongoDb, to help with redundancy and reduce load on the systen
// </summary>

public class MasterNodeService : MasterNode.MasterNodeBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MasterNodeService> _logger;
    private readonly MetricsCollector _metrics;
    private readonly HelperFunctions _helper;

    public MasterNodeService(MongoDbService mongoDbService, ILogger<MasterNodeService> logger, MetricsCollector metrics, HelperFunctions helper)
    {
        _mongoDbService = mongoDbService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _helper = helper;
    }

    // When called will create a new worker node document in MongoDB to be used. Requires a https address to register a worker
    public override async Task<CreateNodeResponse> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to CreateNode call.");
        _metrics.GrpcCallsCounter.WithLabels("CreateNode").Inc();
        var timer = Stopwatch.StartNew();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { await _helper.RunProcess("powershell", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\createWorker.ps1"); }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { await _helper.RunProcess("bash", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\createWorker.sh"); }
        else { _logger.LogError("Unable to add start worker node"); }
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
            _metrics.ErrorCount.WithLabels("CreateNode").Inc();
            _metrics.RequestDuration.WithLabels("CreateNode").Observe(timer.Elapsed.TotalSeconds);
            return new CreateNodeResponse { Status = false, Message = "Failed to retrieve worker resources." };
        }

        _logger.LogInformation("Successfully created node.");
        var updateResourceResult = await _mongoDbService.UpdateWorkerMetadata(
            request.WorkerAddress,
            "waiting",
            resourceResponse.CpuUsage,
            resourceResponse.MemoryUsage,
            resourceResponse.DiskSpace);

        if (updateResourceResult)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { await _helper.RunProcess("powershell", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\update.ps1", "CreateNode"); }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { await _helper.RunProcess("bash", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\update.sh", "CreateNode"); }
            else { _logger.LogError("Unable to add scrape target"); }
            _logger.LogInformation("Successfully updated worker metadata with resource usage.");
            _metrics.RequestDuration.WithLabels("CreateNode").Observe(timer.Elapsed.TotalSeconds);
            return new CreateNodeResponse { Status = true, Message = "Node created successfully with resources." };
        }
        else
        {
            _logger.LogError("Failed to update worker metadata in MongoDB.");
            _metrics.ErrorCount.WithLabels("CreateNode").Inc();
            _metrics.RequestDuration.WithLabels("CreateNode").Observe(timer.Elapsed.TotalSeconds);
            return new CreateNodeResponse { Status = false, Message = "Failed to update worker metadata in MongoDB." };
        }
    }

    // When called it will delete the worker node from the database so it is no longer an active worker. Requires a https string to remove a node
    public override async Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to DeleteNode call");
        _metrics.GrpcCallsCounter.WithLabels("DeleteNode").Inc();
        var timer = Stopwatch.StartNew();
        int workerPid = await _mongoDbService.GetWorkerPid(request.WorkerAddress);
        Process process = Process.GetProcessById(workerPid);
        if (!process.HasExited)
        {
            process.Kill();
            Console.WriteLine($"Process with PID {workerPid} has been terminated.");
        }
        else
        {
            Console.WriteLine($"Process with PID {workerPid} has already exited.");
        }

        bool response = await _mongoDbService.DeleteNode(request.WorkerAddress);
        if (response)
        {
            _logger.LogInformation("Successfully deleted node");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { await _helper.RunProcess("powershell", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\scrape.ps1", "DeleteNode"); }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { await _helper.RunProcess("bash", request.WorkerAddress.ToString(), "..\\..\\..\\scripts\\scrape.sh", "DeleteNode"); }
            else { _logger.LogError("Unable to remove scrape target"); }
            _metrics.RequestDuration.WithLabels("DeleteNode").Observe(timer.Elapsed.TotalSeconds);
            return new DeleteNodeResponse { Status = true, Message = "Node deleted successfully." };
        }
        else
        {
            _logger.LogError("Failed to delete node");
            _metrics.ErrorCount.WithLabels("DeleteNode").Inc();
            _metrics.RequestDuration.WithLabels("DeleteNode").Observe(timer.Elapsed.TotalSeconds);
            return new DeleteNodeResponse { Status = false, Message = "Node failed to be deleted." };
        }
    }

    // When called will store files using a worker node. This function will use node balancing techniques to ensure minimal latency.
    public override async Task<SingleStoreResponse> SingleStore(SingleStoreRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to SingleStore call");
        _metrics.GrpcCallsCounter.WithLabels("SingleStore").Inc();
        var timer = Stopwatch.StartNew();
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

                var resourceRequest = new ResourceUsageRequest { WorkerAddress = worker };
                var resourceResponse = await client.ResourceUsageAsync(resourceRequest);

                var updateMongo = await _mongoDbService.UpdateWorkerMetadata(worker, "waiting", resourceResponse.CpuUsage, resourceResponse.MemoryUsage, resourceResponse.DiskSpace);

                if (updateMongo)
                {
                    _logger.LogInformation("Successfully stored file chunks");
                    _metrics.RequestDuration.WithLabels("HandleFiles").Observe(timer.Elapsed.TotalSeconds);
                    return new SingleStoreResponse { Status = true, Message = workerResponse.Message };
                }
                else
                {
                    _logger.LogError("Failed to update MongoDB with worker's new metadata");
                    _metrics.ErrorCount.WithLabels("HandleFiles").Inc();
                    _metrics.RequestDuration.WithLabels("HandleFiles").Observe(timer.Elapsed.TotalSeconds);
                    return new SingleStoreResponse { Status = false, Message = "Failed to update MongoDB." };
                }
            }
            else
            {
                _logger.LogError("Failed to store chunk at worker node.");
                _metrics.ErrorCount.WithLabels("HandleFiles").Inc();
                _metrics.RequestDuration.WithLabels("HandleFiles").Observe(timer.Elapsed.TotalSeconds);
                return new SingleStoreResponse { Status = false, Message = "Failed to store chunk at worker node." };
            }
        }
        else
        {
            _logger.LogError("No optimal worker found.");
            _metrics.ErrorCount.WithLabels("HandleFiles").Inc();
            _metrics.RequestDuration.WithLabels("HandleFiles").Observe(timer.Elapsed.TotalSeconds);
            return new SingleStoreResponse { Status = false, Message = "Failed to find optimal worker to store your files." };
        }
    }

    // When this function is called given a filename, it will return the location addresses of all chunks this file is stored to
    public override async Task<ChunkLocationsResponse> ChunkLocations(ChunkLocationsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to ChunkLocations call");
        _metrics.GrpcCallsCounter.WithLabels("ChunkLocations").Inc();
        var timer = Stopwatch.StartNew();
        var workerChunks = await _mongoDbService.GetWorkersByFileName(request.FileName);
        var response = new ChunkLocationsResponse();
        if (workerChunks == null || !workerChunks.Any()) { response.Status = false; response.Message = "No workers found for the specified file.";  }
        else { response.Status = true; response.Message = "Successfully retrieved chunk locations."; _metrics.ErrorCount.WithLabels("ChunkLocations").Inc(); }

        foreach (var worker in workerChunks)
        {
            response.WorkerAddress.Add(worker.Key);
            response.ChunkId.AddRange(worker.Value);
        }
        _metrics.RequestDuration.WithLabels("ChunkLocations").Observe(timer.Elapsed.TotalSeconds);
        return response;
    }

    // Lists all the known filenames in the network
    public override async Task<ListFilesResponse> ListFiles(ListFilesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to ListFiles call");
        _metrics.GrpcCallsCounter.WithLabels("ListFiles").Inc();
        var timer = Stopwatch.StartNew();
        var files = await _mongoDbService.GetAllFiles();
        var response = new ListFilesResponse();
        response.FileName.AddRange(files);
        if (files == null || !files.Any()) { response.Status = false; response.Message = "No files found in the database"; _metrics.ErrorCount.WithLabels("ListFiles").Inc(); }
        else { response.Status = true; response.Message = "Successfully retrieved files"; }
        _metrics.RequestDuration.WithLabels("ListFiles").Observe(timer.Elapsed.TotalSeconds);
        return response;
    }

    // This function will interact with a given worker node's address to get it's current resource amounts
    public override async Task<GetWorkerResourcesResponse> GetWorkerResources(GetWorkerResourcesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to GetWorkerResources call");
        _metrics.GrpcCallsCounter.WithLabels("GetWorkerResources").Inc();
        var timer = Stopwatch.StartNew();
        var channel = GrpcChannel.ForAddress(request.WorkerAddress);
        var client = new WorkerNode.WorkerNodeClient(channel);
        var workerRequest = new ResourceUsageRequest { WorkerAddress = request.WorkerAddress };
        var workerResponse = await client.ResourceUsageAsync(workerRequest);
        if (workerResponse.Status == false) { _metrics.ErrorCount.WithLabels("GetWorkerResources").Inc(); }
        _metrics.RequestDuration.WithLabels("GetWorkerResources").Observe(timer.Elapsed.TotalSeconds);
        return new GetWorkerResourcesResponse
        {
            Status = workerResponse.Status,
            Message = workerResponse.Message,
            CpuUsage = workerResponse.CpuUsage,
            MemoryUsage = workerResponse.MemoryUsage,
            DiskSpace = workerResponse.DiskSpace
        };
    }

    // This function allows users to distribute their data to various workers allowing for redundancy
    public override async Task<DistributeFileResponse> DistributeFile(DistributeFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to DistributedFile call");
        _metrics.GrpcCallsCounter.WithLabels("DistributeFile").Inc();
        var timer = Stopwatch.StartNew();

        try
        {
            if (request.FileData == null || request.FileData.Length == 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "File data cannot be null or empty."));
            }

            var fileBytes = request.FileData.ToByteArray();
            int fileSize = fileBytes.Length;

            var availableWorkers = await _helper.GetAvailableWorkers();
            if (availableWorkers.Count == 0)
            {
                _logger.LogWarning("No available workers to distribute the file");
                return new DistributeFileResponse
                {
                    Status = false,
                    Message = "No available workers to process the file."
                };
            }

            _logger.LogInformation($"Found {availableWorkers.Count} available workers");

            var chunks = _helper.SplitFileToChunks(fileBytes, availableWorkers.Count);
            _logger.LogInformation($"File split into {chunks.Count} chunks");

            var tasks = new List<Task>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var worker = availableWorkers[i % availableWorkers.Count];
                var chunk = chunks[i];
                var chunkId = Guid.NewGuid().ToString();
                tasks.Add(DistributeChunkToWorker(worker, chunk, chunkId, request.FileName));
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("Completed distributing all chunks");

            timer.Stop();
            _metrics.RequestDuration.WithLabels("DistributeFile").Observe(timer.Elapsed.TotalSeconds);

            return new DistributeFileResponse
            {
                Status = true,
                Message = "File distributed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file distribution");
            _metrics.ErrorCount.WithLabels("DistributeFile").Inc();
            return new DistributeFileResponse
            {
                Status = false,
                Message = $"Error during file distribution: {ex.Message}"
            };
        }
    }

    // This funciton will return the files data from all workers storing this data
    public override async Task<RetrieveFileResponse> RetrieveFile(RetrieveFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to RetrieveFile call");
        _metrics.GrpcCallsCounter.WithLabels("RetrieveFile").Inc();
        var timer = Stopwatch.StartNew();

        try
        {
            // Call the MongoDB service to retrieve file metadata and chunk information
            var workerChunksWithData = await _mongoDbService.RetrieveFileFromWorkers(request.FileName);

            if (workerChunksWithData.Count == 0)
            {
                timer.Stop();
                _metrics.RequestDuration.WithLabels("RetrieveFile").Observe(timer.Elapsed.TotalSeconds);
                return new RetrieveFileResponse
                {
                    Status = false,
                    Message = "No chunks found for the requested file."
                };
            }

            var chunksList = new List<ByteString>();

            foreach (var worker in workerChunksWithData)
            {
                string workerAddress = worker.Key;
                var chunkDataList = worker.Value;

                var channel = GrpcChannel.ForAddress(workerAddress);
                var client = new WorkerNode.WorkerNodeClient(channel);

                foreach (var chunk in chunkDataList)
                {
                    var chunkRequest = new GetChunkRequest { ChunkId = chunk };
                    var response = await client.GetChunkAsync(chunkRequest);

                    if (response?.ChunkData != null)
                    {
                        chunksList.Add(response.ChunkData);
                    }
                }
            }

            timer.Stop();
            _metrics.RequestDuration.WithLabels("RetrieveFile").Observe(timer.Elapsed.TotalSeconds);

            return new RetrieveFileResponse
            {
                Status = true,
                Message = "File retrieved successfully",
                FileName = request.FileName,
                FileData = { chunksList }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file.");
            timer.Stop();
            _metrics.ErrorCount.WithLabels("RetrieveFile").Inc();
            _metrics.RequestDuration.WithLabels("RetrieveFile").Observe(timer.Elapsed.TotalSeconds);

            return new RetrieveFileResponse
            {
                Status = false,
                Message = $"Error retrieving file: {ex.Message}"
            };
        }
    }

    // Deletes files from workers by giving a file name
    public override async Task<DeleteFileResponse> DeleteFile(DeleteFileRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to DeleteFile call");
        _metrics.GrpcCallsCounter.WithLabels("DeleteFile").Inc();
        var timer = Stopwatch.StartNew();
        var chunkIds = await _mongoDbService.GetChunkIdsForFile(request.FileName);

        if (chunkIds.Count == 0)
        {
            _metrics.RequestDuration.WithLabels("DeleteFile").Observe(timer.Elapsed.TotalSeconds);
            return new DeleteFileResponse
            {
                Status = false,
                Message = "No chunks found for the specified file."
            };
        }

        var workersWithFileRemoved = await _mongoDbService.RemoveFileFromWorkers(request.FileName);

        if (workersWithFileRemoved.Count == 0)
        {
            _metrics.RequestDuration.WithLabels("DeleteFile").Observe(timer.Elapsed.TotalSeconds);
            return new DeleteFileResponse
            {
                Status = false,
                Message = "No workers found with the specified file or file already removed."
            };
        }

        var chunkDeletionResults = new List<string>();

        foreach (var workerAddress in workersWithFileRemoved)
        {
            var channel = GrpcChannel.ForAddress(workerAddress);
            var client = new WorkerNode.WorkerNodeClient(channel);

            try
            {
                foreach (var chunkId in chunkIds)
                {
                    var deleteChunkRequest = new DeleteChunkRequest { ChunkId = chunkId };
                    var deleteChunkResponse = client.DeleteChunk(deleteChunkRequest);
                    if (deleteChunkResponse.Status)
                    {
                        chunkDeletionResults.Add($"Successfully deleted chunk {chunkId} from worker {workerAddress}");
                    }
                    else
                    {
                        chunkDeletionResults.Add($"Failed to delete chunk {chunkId} from worker {workerAddress}: {deleteChunkResponse.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _metrics.ErrorCount.WithLabels("DeleteFile").Inc();
                _metrics.RequestDuration.WithLabels("DeleteFile").Observe(timer.Elapsed.TotalSeconds);
                chunkDeletionResults.Add($"Error calling DeleteChunk on worker {workerAddress}: {ex.Message}");
            }
        }
        var deletionMessage = string.Join("\n", chunkDeletionResults);
        _metrics.RequestDuration.WithLabels("DeleteFile").Observe(timer.Elapsed.TotalSeconds);
        return new DeleteFileResponse
        {
            Status = true,
            Message = $"File {request.FileName} deletion results:\n{deletionMessage}"
        };
    }
    private async Task DistributeChunkToWorker(string worker, byte[] chunk, string chunkId, string fileName)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            var channel = GrpcChannel.ForAddress(worker);
            var client = new WorkerNode.WorkerNodeClient(channel);

            await _mongoDbService.UpdateWorkerStatus(worker, "working", fileName, chunkId);

            var workerRequest = new StoreChunkRequest { ChunkId = chunkId, ChunkData = ByteString.CopyFrom(chunk) };
            var workerResponse = client.StoreChunk(workerRequest);

            if (workerResponse.Status)
            {
                _logger.LogInformation($"Chunk {chunkId} stored successfully at worker {worker}");

                var resourceRequest = new ResourceUsageRequest { WorkerAddress = worker };
                var resourceResponse = await client.ResourceUsageAsync(resourceRequest);

                await _mongoDbService.UpdateWorkerMetadata(
                    worker,
                    "waiting",
                    resourceResponse.CpuUsage,
                    resourceResponse.MemoryUsage,
                    resourceResponse.DiskSpace
                );
            }
            else { _logger.LogError($"Failed to store chunk {chunkId} on worker {worker}"); }
            _metrics.RequestDuration.WithLabels("DistributeChunkToWorker").Observe(timer.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error distributing chunk {chunkId} to worker {worker}");
        }
    }
}