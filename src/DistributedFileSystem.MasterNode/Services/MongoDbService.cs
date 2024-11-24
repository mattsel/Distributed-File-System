using MongoDB.Driver;
using DistributedFileSystem.MasterNode.Models;

// <summary>
// This acts as a helper funciton for the master node to use as a memory storage.
// This namespace is vital to ensure redundancy in the system for the master ndoe to keep track of all metadata
// and worker nodes available in the network.
// </summary>

namespace DistributedFileSystem.MasterNode.Services
{
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<WorkerMetadata> _workerCollection;
        private readonly ILogger<MongoDbService> _logger;

        public MongoDbService(IConfiguration configuration, ILogger<MongoDbService> logger)
        {
            var mongoSettings = configuration.GetSection("MongoDbSettings");
            var connectionString = mongoSettings.GetValue<string>("ConnectionString");
            var databaseName = mongoSettings.GetValue<string>("Database");
            var collectionName = mongoSettings.GetValue<string>("Collection");
            var mongoClient = new MongoClient(connectionString);
            _database = mongoClient.GetDatabase(databaseName);
            _workerCollection = _database.GetCollection<WorkerMetadata>(collectionName);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Called to initialize a document for new nodes
        public async Task<bool> CreateNode(string workerAddress)
        {
            _logger.LogInformation("Beginning to create new worker node.");
            var workerMetadata = new WorkerMetadata
            {
                WorkerAddress = workerAddress,
                Status = "waiting",
                LastUpdated = DateTime.UtcNow,
                Files = new List<FileMetadata>()
            };
            try { await _workerCollection.InsertOneAsync(workerMetadata); _logger.LogInformation("Worker node created successfully."); return true;}
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
        public async Task<bool> UpdateWorkerMetadataAsync(string workerAddress, string status, float cpuUsage, float memoryUsage, float diskSpace)
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
        public async Task<Dictionary<string, List<string>>> GetWorkersByFileNameAsync(string fileName)
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
    }
}