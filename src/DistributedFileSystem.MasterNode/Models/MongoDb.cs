namespace DistributedFileSystem.MasterNode.Models
{
    public class WorkerMetadata
    {
        public string? WorkerAddress { get; set; }
        public Dictionary<string, Dictionary<string, string>>? Chunks { get; set; }
        public string? Status { get; set; }
        public float DiskSpace { get; set; }
        public float CpuUsage { get; set; }
        public float MemoryUsage { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
