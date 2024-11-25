using DistributedFileSystem.MasterNode;
using Grpc.Core;
using Grpc.Net.Client;
using DistributedFileSystem.MasterNode.Services;
using DistributedFileSystem.WorkerNode;
using System.Diagnostics;
using DistributedFileSystem.MasterNode.Models;
using System.Runtime.InteropServices;

// <summary>
// This class acts as the translator for gRPC calls to the master node. Many interactions with the master node will require the master node to interact with
// worker nodes to properly assist the client's needs. Much of it's memory is stored in MongoDb, to help with redundancy and reduce load on the systen
// </summary>

public class MasterNodeService : MasterNode.MasterNodeBase
{
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<MasterNodeService> _logger;
    private readonly MetricsCollector _metrics;

    public MasterNodeService(MongoDbService mongoDbService, ILogger<MasterNodeService> logger, MetricsCollector metrics)
    {
        _mongoDbService = mongoDbService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
    }

    // When called will create a new worker node document in MongoDB to be used. Requires a https address to register a worker
    public override async Task<CreateNodeResponse> CreateNode(CreateNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to CreateNode call.");
        _metrics.GrpcCallsCounter.WithLabels("CreateNode").Inc();
        var timer = Stopwatch.StartNew();

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

        bool response = await _mongoDbService.CreateNode(request.WorkerAddress);

        if (response)
        {
            _logger.LogInformation("Successfully created node.");
            var updateResourceResult = await _mongoDbService.UpdateWorkerMetadata(
                request.WorkerAddress,
                "waiting",
                resourceResponse.CpuUsage,
                resourceResponse.MemoryUsage,
                resourceResponse.DiskSpace);

            if (updateResourceResult)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { AddScrape("powershell", request.WorkerAddress.ToString(), "CreateNode"); }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { AddScrape("bash", request.WorkerAddress.ToString(), "CreateNode"); }
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
        else
        {
            _logger.LogError("Failed to create node.");
            _metrics.ErrorCount.WithLabels("CreateNode").Inc();
            _metrics.RequestDuration.WithLabels("CreateNode").Observe(timer.Elapsed.TotalSeconds);
            return new CreateNodeResponse { Status = false, Message = "Node creation failed." };
        }
    }

    // When called it will delete the worker node from the database so it is no longer an active worker. Requries a https string to remove a node
    public override async Task<DeleteNodeResponse> DeleteNode(DeleteNodeRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Responding to DeleteNode call");
        _metrics.GrpcCallsCounter.WithLabels("DeleteNode").Inc();
        var timer = Stopwatch.StartNew();
        bool response = await _mongoDbService.DeleteNode(request.WorkerAddress);
        if (response)
        {
            _logger.LogInformation("Successfully deleted node");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) { AddScrape("powershell", request.WorkerAddress.ToString(), "DeleteNode"); }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) { AddScrape("bash", request.WorkerAddress.ToString(), "DeleteNode"); }
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
        _logger.LogInformation("Responding to HandleFiles call");
        _metrics.GrpcCallsCounter.WithLabels("HandleFiles").Inc();
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
    private void AddScrape(string language, string address, string action)
    {
        try
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath;
            string absoluteScriptPath;
            string args;
            if (language == "powershell")
            {
                scriptPath = "..\\..\\..\\scripts\\update.ps1";
                absoluteScriptPath = Path.GetFullPath(Path.Combine(baseDirectory, scriptPath));
                args = $"{absoluteScriptPath} -address {address} -action {action}";
            }
            else
            {
                scriptPath = "..\\..\\..\\scripts\\update.sh";
                absoluteScriptPath = Path.GetFullPath(Path.Combine(baseDirectory, scriptPath));
                args = $"{absoluteScriptPath} {address} {action}";
            }
            
            _logger.LogInformation($"Base directory: {baseDirectory}");
            _logger.LogInformation($"Script path: {absoluteScriptPath}");
            _logger.LogInformation($"Absolute script path: {absoluteScriptPath}");
            _logger.LogInformation(args);

            if (!File.Exists(absoluteScriptPath)) { _logger.LogError("Script file not found at: " + absoluteScriptPath); return; }
            var processStartInfo = new ProcessStartInfo
            {
                FileName = language,
                Arguments = $"{absoluteScriptPath} -address {address} -action {action}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = new Process { StartInfo = processStartInfo };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();

            if (!string.IsNullOrEmpty(output)) { _logger.LogInformation(output); }
            if (!string.IsNullOrEmpty(errors)) { _logger.LogError(errors); }
            process.WaitForExit();
        }
        catch (Exception ex) { _logger.LogError($"Failed to add scrape: {ex}"); }
    }
}