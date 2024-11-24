namespace DistributedFileSystem.MasterNode.Models
{
    public class WorkerMetadata
    {
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
