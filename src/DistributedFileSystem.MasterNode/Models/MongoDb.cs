namespace DistributedFileSystem.MasterNode.Models
{
    public class WorkerMetadata
    {
        public int WorkerId { get; set; }
        public string? WorkerAddress { get; set; }
        public List<string>? Chunks { get; set; }
        public string? Status { get; set; }
        public int DiskSpace {  get; set; }
        public int CpuUsage { get; set; }
        public int MemoryUsage { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}