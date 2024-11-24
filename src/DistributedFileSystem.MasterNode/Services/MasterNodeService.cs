using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using DistributedFileSystem.MasterNode.Models;
using Microsoft.Extensions.Logging;

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

        public async Task<bool> CreateNode(string workerAddress)
        {
            var workerMetadata = new WorkerMetadata
            {
                WorkerAddress = workerAddress,
                Status = "waiting",
                LastUpdated = DateTime.UtcNow,
                Chunks = new Dictionary<string, Dictionary<string, string>>()
            };
            try { await _workerCollection.InsertOneAsync(workerMetadata); return true; }
            catch { return false; }
        }

        public async Task<bool> DeleteNode(string workerAddress)
        {
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            try { await _workerCollection.DeleteOneAsync(filter); return true; }
            catch { return false; }
        }

        public async Task<bool> UpdateWorkerMetadataAsync(string workerAddress, string status, int diskSpace, int cpuUsage, int memoryUsage)
        {
            var update = Builders<WorkerMetadata>.Update
                .Set(w => w.DiskSpace, diskSpace)
                .Set(w => w.Status, status)
                .Set(w => w.CpuUsage, cpuUsage)
                .Set(w => w.MemoryUsage, memoryUsage)
                .Set(w => w.LastUpdated, DateTime.UtcNow);
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            try { await _workerCollection.UpdateOneAsync(filter, update); return true; }
            catch { return false; }
        }

        public async Task<bool> UpdateWorkerStatus(string workerAddress, string status)
        {
            var update = Builders<WorkerMetadata>.Update
                .Set(w => w.Status, status)
                .Set(w => w.LastUpdated, DateTime.UtcNow);
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            try { await _workerCollection.UpdateOneAsync(filter, update); return true; }
            catch { return false; }
        }

        public async Task<List<string?>> GetChunkLocations(string chunkId)
        {
            var filter = Builders<WorkerMetadata>.Filter.Where(w => w.Chunks.Values.Any(chunk => chunk.ContainsKey(chunkId)));
            var projection = Builders<WorkerMetadata>.Projection.Expression(w => w.WorkerAddress);
            var workersWithChunkId = await _workerCollection.Find(filter).Project(projection).ToListAsync();
            return workersWithChunkId;
        }

        public async Task<WorkerMetadata> GetWorkerMetadataAsync(string workerAddress)
        {
            var filter = Builders<WorkerMetadata>.Filter.Eq(w => w.WorkerAddress, workerAddress);
            return await _workerCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<string?>> GetAllWorkerAddressesAsync()
        {
            var projection = Builders<WorkerMetadata>.Projection.Expression(w => w.WorkerAddress);
            return await _workerCollection
                .Find(_ => true)
                .Project(projection)
                .ToListAsync();
        }

        public async Task<List<string?>> GetAllFiles()
        {
            var projection = Builders<WorkerMetadata>.Projection.Expression(w => w.Chunks);
            var workersWithChunks = await _workerCollection
                .Find(_ => true)
                .Project(projection)
                .ToListAsync();
            return workersWithChunks.SelectMany(worker => worker.Chunks.Keys).ToList();
        }

        public async Task<string?> GetOptimalWorker(int chunkSize)
        {
            _logger.LogInformation("Fetching most optimal worker with status 'waiting' and sufficient disk space.");
            var workers = await _workerCollection
                .Find(w => w.Status == "waiting")
                .ToListAsync();

            if (!workers.Any()) { _logger.LogWarning("No workers with status 'waiting' found."); return null; }

            var validWorkers = workers.Where(w => w.DiskSpace >= chunkSize).ToList();
            if (!validWorkers.Any()) { _logger.LogWarning($"No workers with sufficient disk space for chunk size {chunkSize}."); return null; }

            var optimalWorker = validWorkers
                .OrderBy(w => Math.Min(Math.Min(w.CpuUsage, w.MemoryUsage), w.DiskSpace))
                .FirstOrDefault();

            if (optimalWorker == null) { _logger.LogWarning("No optimal worker found."); return null; }

            _logger.LogInformation($"Optimal worker found: {optimalWorker.WorkerAddress} (CPU: {optimalWorker.CpuUsage}%, Memory: {optimalWorker.MemoryUsage}%, Disk: {optimalWorker.DiskSpace}MB)");
            return optimalWorker.WorkerAddress;
        }
    }
}