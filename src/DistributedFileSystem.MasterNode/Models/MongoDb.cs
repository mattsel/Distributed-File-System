using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

// <summary>
// Model that is used to store metadata for worker node's information. This model includes
// the ability ot track resource usage, chunks and file allocation inside of each worker
// </summary>

namespace DistributedFileSystem.MasterNode.Models
{
    public class WorkerMetadata
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public string WorkerAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public float DiskSpace { get; set; }
        public float CpuUsage { get; set; }
        public float MemoryUsage { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<FileMetadata> Files { get; set; } = new List<FileMetadata>();
    }

    public class FileMetadata
    {
        public string FileName { get; set; } = string.Empty;
        public Dictionary<string, string> Chunks { get; set; } = new Dictionary<string, string>();
    }
}
