using MongoDB.Driver;
using DistributedFileSystem.MasterNode.Models;
using Prometheus;

// <summary>
// This acts as a helper funciton for the master node to use as a memory storage.
// This namespace is vital to ensure redundancy in the system for the master ndoe to keep track of all metadata
// and worker nodes available in the network.
// </summary>

namespace DistributedFileSystem.MasterNode.Helpers
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<WorkerMetadata> _workerCollection;
        private readonly ILogger<MongoDbService> _logger;
        private readonly MetricsCollector _metrics;
        private readonly IMongoCollection<Counter> _counterCollection;

        public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger, MetricsCollector metrics)
        {
            var mongoSettings = configuration.GetSection("MongoDbSettings");
            var connectionString = mongoSettings.GetValue<string>("ConnectionString");
            var databaseName = mongoSettings.GetValue<string>("Database");
            var collectionName = mongoSettings.GetValue<string>("Collection");
            var counterName = mongoSetting.GetValue<string>("CounterCollection")
            var mongoClient = new MongoClient(connectionString);
            _database = mongoClient.GetDatabase(databaseName);
            _workerCollection = _database.GetCollection<WorkerMetadata>(collectionName);
            _counterCollection = _database.GetCollection<Counter>(counterName)
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        }

        // Called to initialize a document for new nodes
        public async Task<bool> CreateNode(string workerAddress)
        {
            try
            {
                _logger.LogInformation("Checking if worker node already exists.");
                var existingNode = await _workerCollection.Find(w => w.WorkerAddress == workerAddress).FirstOrDefaultAsync();
                if (existingNode != null) { _logger.LogInformation("Worker node already exists. Skipping creation."); return false; }

                var workerMetadata = new WorkerMetadata
                {
                    WorkerAddress = workerAddress,
                    Status = "waiting",
                    LastUpdated = DateTime.UtcNow,
                    Files = new List<FileMetadata>()
                };
                await _workerCollection.InsertOneAsync(workerMetadata);
                _logger.LogInformation("Worker node created successfully.");
                return true;
            }
            catch (Exception ex) { _logger.LogError($"Failed to create new worker node: {ex}"); return false; }
        }

        // Called to delete worker node's document from the database
        public async Task<bool> DeleteNode(string workerAddress)
        {
            _logger.LogInformation("Beginning to delete worker node.");
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            try { await _workerCollection.DeleteOneAsync(filter); _logger.LogInformation("Worker node deleted successfully."); return true; }
            catch (Exception ex) { _logger.LogError($"Failed to delete worker node: {ex}"); return false; }
        }

        // This funciton allows the master node to update metadata given the address of the worker node
        public async Task<bool> UpdateWorkerMetadata(string workerAddress, string status, float cpuUsage, float memoryUsage, float diskSpace)
        {
            _logger.LogInformation("Beginning to update worker's metadata");
            var update = Builders<WorkerMetadata>.Update
                .Set(w => w.DiskSpace, diskSpace)
                .Set(w => w.Status, status)
                .Set(w => w.CpuUsage, cpuUsage)
                .Set(w => w.MemoryUsage, memoryUsage)
                .Set(w => w.LastUpdated, DateTime.UtcNow);
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            try { await _workerCollection.UpdateOneAsync(filter, update); _logger.LogInformation("Successfully updated worker nodes metadata"); return true; }
            catch (Exception ex) { _logger.LogError($"Failed to update worker's metadata: {ex}"); return false; }
        }

        // Allows the master node to update the worker status to either be working or waiting. This helps the master node to load balance
        public async Task<bool> UpdateWorkerStatus(string workerAddress, string status, string fileName, string chunkId)
        {

            _logger.LogInformation("Beginning to update worker's status");
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            var update = Builders<WorkerMetadata>.Update.Combine(
                Builders<WorkerMetadata>.Update.Set(w => w.Status, status),
                Builders<WorkerMetadata>.Update.Set(w => w.LastUpdated, DateTime.UtcNow),
                Builders<WorkerMetadata>.Update.AddToSet(w => w.Files, new FileMetadata
                {
                    FileName = fileName,
                    Chunks = new Dictionary<string, string> { { chunkId, workerAddress } }
                })
            );
            try
            {
                var result = await _workerCollection.UpdateOneAsync(filter, update);
                _logger.LogInformation("Successfully updated worker's status");
                return result.ModifiedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unable to update worker's status: {ex}");
                return false;
            }
        }

        // Given a filename, this query will return the addresses of all worker's that are storing the file's chunks
        public async Task<Dictionary<string, List<string>>> GetWorkersByFileName(string fileName)
        {
            _logger.LogInformation($"Beginning to get all worker address that are storing chunks of filename: {fileName}");
            var filter = Builders<WorkerMetadata>.Filter.ElemMatch(w => w.Files, f => f.FileName == fileName);
            var projection = Builders<WorkerMetadata>.Projection.Include(w => w.WorkerAddress).Include(w => w.Files);
            var workers = await _workerCollection.Find(filter).Project<WorkerMetadata>(projection).ToListAsync();

            var result = new Dictionary<string, List<string>>();
            foreach (var worker in workers)
            {
                var matchingFile = worker.Files.FirstOrDefault(f => f.FileName == fileName);
                if (matchingFile != null)
                {
                    result[worker.WorkerAddress] = matchingFile.Chunks.Keys.ToList();
                }
            }
            _logger.LogInformation("Finished finding all workers by filename");
            return result;
        }

        // Query to return all files in the network
        public async Task<List<string>> GetAllFiles()
        {
            _logger.LogInformation("Beginning to get all files");
            var projection = Builders<WorkerMetadata>.Projection.Include(w => w.Files);
            var workers = await _workerCollection.Find(_ => true).Project<WorkerMetadata>(projection).ToListAsync();
            _logger.LogInformation("Finished getting all files");
            return workers.SelectMany(worker => worker.Files.Select(file => file.FileName)).ToList();
        }

        // This uses worker's resources and status to help choose a worker that can handle the request with the least amount of latency possible
        public async Task<string?> GetOptimalWorker(int chunkSize)
        {
            _logger.LogInformation("Fetching most optimal worker with status 'waiting' and sufficient disk space.");

            var workers = await _workerCollection.Find(w => w.Status == "waiting").ToListAsync();
            if (!workers.Any())
            {
                _logger.LogWarning("No workers with status 'waiting' found.");
                return null;
            }

            var validWorkers = workers.Where(w => w.DiskSpace >= chunkSize).ToList();
            if (!validWorkers.Any())
            {
                _logger.LogWarning($"No workers with sufficient disk space for chunk size {chunkSize}.");
                return null;
            }

            var optimalWorker = validWorkers
                .OrderBy(w => Math.Min(Math.Min(w.CpuUsage, w.MemoryUsage), w.DiskSpace))
                .FirstOrDefault();
            if (optimalWorker == null)
            {
                _logger.LogWarning("No optimal worker found.");
                return null;
            }

            _logger.LogInformation($"Optimal worker found: {optimalWorker.WorkerAddress} (CPU: {optimalWorker.CpuUsage}%, Memory: {optimalWorker.MemoryUsage}%, Disk: {optimalWorker.DiskSpace}MB)");
            return optimalWorker.WorkerAddress;
        }

        // This function will set a pid to the workers metadata to later be retrieved to kill processes of a worker node
        public async Task<bool> SetWorkerPid(string workerAddress, int pid)
        {
            _logger.LogInformation($"Setting pid ({pid}) to worker at the address: {workerAddress} .");
            var response = await _workerCollection.Find(w => w.WorkerAddress.Trim() == workerAddress.Trim()).ToListAsync();
            _logger.LogInformation($"{response.Count}");
            if (response.Count == 1)
            {
                var worker = response[0];
                worker.WorkerPid = pid;
                var updateResult = await _workerCollection.ReplaceOneAsync(
                    w => w.WorkerAddress == workerAddress,
                    worker
                );

                if (updateResult.IsAcknowledged && updateResult.ModifiedCount > 0)
                {
                    _logger.LogInformation($"Successfully updated PID to {pid} for worker at {workerAddress}.");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"Failed to update PID for worker at {workerAddress}.");
                    return false;
                }
            }
            else
            {
                _logger.LogWarning($"No worker found at address {workerAddress} or multiple workers found.");
                return false;
            }
        }

        // Function called to get a workers pid
        public async Task<int> GetWorkerPid(string workerAddress)
        {
            var response = await _workerCollection.Find(w => w.WorkerAddress == workerAddress).ToListAsync();
            if (response.Count == 1)
            {
                return response[0].WorkerPid;
            }
            else
            {
                return 0;
            }
        }

        // Returns a list of all worker addresses that are currently waiting
        public async Task<List<string>> GetAllAvailableWorkers()
        {
            var available = new List<string>();
            var response = await _workerCollection.Find(w => w.Status == "waiting").ToListAsync();
            foreach (var worker in response)
            {
                available.Add(worker.WorkerAddress);
            }
            return available;
        }

        // This will retrieve the file from all workers that contain the filename
        public async Task<List<KeyValuePair<string, List<string>>>> RetrieveFileFromWorkers(string fileName)
        {
            var workerChunksList = new List<KeyValuePair<string, List<string>>>();
            var workers = await _workerCollection
                .Find(worker => worker.Files.Exists(f => f.FileName == fileName))
                .ToListAsync();

            foreach (var worker in workers)
            {
                var file = worker.Files.Find(f => f.FileName == fileName);

                if (file != null)
                {
                    var chunkIds = file.Chunks.Keys.ToList();
                    var sortedChunks = chunkIds.OrderBy(chunkKey => chunkKey).ToList();
                    workerChunksList.Add(new KeyValuePair<string, List<string>>(worker.WorkerAddress, sortedChunks));
                }
            }

            return workerChunksList;
        }

        // Gets the chunkId given a filename to later make an rpc call to get a chunk from worker node
        public async Task<List<string>> GetChunkIdsForFile(string fileName)
        {
            var workersWithFile = await _workerCollection
                .Find(worker => worker.Files.Any(f => f.FileName == fileName))
                .ToListAsync();

            var chunkIds = new List<string>();

            foreach (var worker in workersWithFile)
            {
                var fileMetadata = worker.Files.FirstOrDefault(f => f.FileName == fileName);
                if (fileMetadata != null)
                {
                    foreach (var chunk in fileMetadata.Chunks.Keys)
                    {
                        chunkIds.Add(chunk);
                    }
                }
            }
            return chunkIds.OrderBy(chunkId => chunkId).ToList();
        }

        // This will remove all instances of a filename from workers that contain this file
        public async Task<List<string>> RemoveFileFromWorkers(string fileName)
        {
            var workersWithFile = await _workerCollection
                .Find(worker => worker.Files.Any(f => f.FileName == fileName))
                .ToListAsync();

            if (workersWithFile.Count == 0)
            {
                return new List<string>();
            }

            var workerAddressesWithFileRemoved = new List<string>();
            foreach (var worker in workersWithFile)
            {
                var updateDefinition = Builders<WorkerMetadata>.Update.PullFilter(
                    w => w.Files,
                    f => f.FileName == fileName
                );

                var updateResult = await _workerCollection.UpdateOneAsync(
                    w => w.Id == worker.Id,
                    updateDefinition
                );

                if (updateResult.ModifiedCount > 0)
                {
                    workerAddressesWithFileRemoved.Add(worker.WorkerAddress);
                }
            }
            return workerAddressesWithFileRemoved;
        }

        public static string GenerateChunkID()
        {
            lock (typeof(SequentialIdGenerator))
            {
                var filter = Builders<Counter>.Filter.Eq(c => c.Name, "sequentialCounter");
                var counterDocument = _counterCollection.Find(filter).FirstOrDefault();

                if (counterDocument == null)
                {
                    counterDocument = new Counter
                    {
                        Name = "sequentialCounter",
                        Value = 0
                    };
                    _counterCollection.InsertOne(counterDocument);
                }
                counterDocument.Value++;
                var update = Builders<Counter>.Update.Set(c => c.Value, counterDocument.Value);
                _counterCollection.UpdateOne(filter, update);

                return counterDocument.Value.ToString("D10");
            }
        }
    }
}
