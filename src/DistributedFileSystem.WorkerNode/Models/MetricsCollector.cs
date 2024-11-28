using Prometheus;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DistributedFileSystem.WorkerNode.Models
{
    public class MetricsCollector
    {
        public Counter GrpcCallsCounter { get; }
        public Counter ErrorCount { get; }
        public Histogram RequestDuration { get; }
        public Gauge CpuUsage { get; }
        public Gauge MemoryUsage { get; }
        public Gauge AvailableDiskSpace { get; }

        public MetricsCollector()
        {
            GrpcCallsCounter = Metrics.CreateCounter(
                "worker_node_grpc_calls_total",
                "Total number of gRPC calls received by the worker node",
                new CounterConfiguration { LabelNames = new[] { "request" } }
            );

            ErrorCount = Metrics.CreateCounter(
                "worker_node_errors_total",
                "Total number of errors encountered by the worker node",
                new CounterConfiguration { LabelNames = new[] { "request" } }
            );

            RequestDuration = Metrics.CreateHistogram(
                "worker_node_request_duration_seconds",
                "Histogram of request durations for the worker node in seconds",
                new HistogramConfiguration { LabelNames = new[] { "request" } }
            );

            CpuUsage = Metrics.CreateGauge(
                "worker_node_cpu_usage_percentage",
                "Current CPU usage percentage of the worker node",
                new GaugeConfiguration { LabelNames = new[] { "worker" } }
            );

            MemoryUsage = Metrics.CreateGauge(
                "worker_node_memory_usage_bytes",
                "Current memory usage (in bytes) of the worker node",
                new GaugeConfiguration { LabelNames = new[] { "worker" } }
            );

            AvailableDiskSpace = Metrics.CreateGauge(
                "worker_node_available_disk_space_bytes",
                "Current available disk space (in bytes) of the worker node",
                new GaugeConfiguration { LabelNames = new[] { "worker" } }
            );
        }
    }
}
using Google.Protobuf;
using Grpc.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DistributedFileSystem.WorkerNode.Models;
using Prometheus;

// <sumary>
// All of the functions in this file serve as a translator for gRPC calls. When these functions are called with their required parameters,
// it will return a RPC responses.
// </summary>

namespace DistributedFileSystem.WorkerNode.Services
{
    public class WorkerNodeService : WorkerNode.WorkerNodeBase
    {
        private readonly string _chunkStorageDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Chunks");
        private readonly DriveInfo _driveInfo;
        private readonly ILogger<WorkerNodeService> _logger;
        private readonly MetricsCollector _metrics;

        public WorkerNodeService(ILogger<WorkerNodeService> logger, MetricsCollector metrics)
        {
            string rootDirectory = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/";
            _driveInfo = new DriveInfo(rootDirectory);
            _logger = logger;
            _metrics = metrics;
        }

        // Stores chunks on the worker node given a unique chunk id 
        public override Task<StoreChunkResponse> StoreChunk(StoreChunkRequest request, ServerCallContext context)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Responding to StoreChunk call");
                _metrics.GrpcCallsCounter.WithLabels("StoreChunk").Inc();
                var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
                Directory.CreateDirectory(_chunkStorageDirectory);
                File.WriteAllBytes(chunkFilePath, request.ChunkData.ToByteArray());
                _metrics.RequestDuration.WithLabels("StoreChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new StoreChunkResponse { Status = true, Message = $"Chunk {request.ChunkId} stored successfully at {chunkFilePath}." });
            }
            catch (Exception ex)
            {
                _metrics.ErrorCount.WithLabels("StoreChunk").Inc();
                _metrics.RequestDuration.WithLabels("StoreChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new StoreChunkResponse { Status = false, Message = $"Chunk {request.ChunkId} failed to be stored: {ex}." });
            }
        }

        // Returns the chunk given the unique chunk id for a given file
        public override Task<GetChunkResponse> GetChunk(GetChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to GetChunk call");
            _metrics.GrpcCallsCounter.WithLabels("GetChunk").Inc();
            var timer = Stopwatch.StartNew();
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                var chunkData = File.ReadAllBytes(chunkFilePath);
                _metrics.RequestDuration.WithLabels("GetChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new GetChunkResponse
                {
                    Status = true,
                    Message = $"Chunk {request.ChunkId} retrieved successfully.",
                    ChunkData = ByteString.CopyFrom(chunkData)
                });
            }
            else
            {
                _logger.LogError("Failed to find ChunkId");
                _metrics.ErrorCount.WithLabels("GetChunk").Inc();
                _metrics.RequestDuration.WithLabels("GetChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new GetChunkResponse { Status = false, Message = $"Chunk {request.ChunkId} not found." });
            }
        }

        // Will remove a chunk from worker node given a unique chunk id
        public override Task<DeleteChunkResponse> DeleteChunk(DeleteChunkRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Responding to DeleteChunk call");
            _metrics.GrpcCallsCounter.WithLabels("DeleteChunk").Inc();
            var timer = Stopwatch.StartNew();
            var chunkFilePath = Path.Combine(_chunkStorageDirectory, request.ChunkId);
            if (File.Exists(chunkFilePath))
            {
                File.Delete(chunkFilePath);
                _logger.LogInformation("Successfully deleted chunk");
                _metrics.RequestDuration.WithLabels("DeleteChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new DeleteChunkResponse { Status = true, Message = $"Chunk {request.ChunkId} deleted successfully." });
            }
            else
            {
                _logger.LogError("Failed to delete chunk");
                _metrics.ErrorCount.WithLabels("DeleteChunk").Inc();
                _metrics.RequestDuration.WithLabels("DeleteChunk").Observe(timer.Elapsed.TotalSeconds);
                return Task.FromResult(new DeleteChunkResponse { Status = false, Message = $"Chunk {request.ChunkId} deletion failed." });
            }
        }

        // Will return the operating system resources like CPU, Memory, and Disk space
        public override Task<ResourceUsageResponse> ResourceUsage(ResourceUsageRequest request, ServerCallContext context)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                _logger.LogInformation("Responding to ResourceUsage call");
                _metrics.GrpcCallsCounter.WithLabels("ResourceUsage").Inc();
                float cpuUsage;
                long memoryUsage;
                long diskSpace = _driveInfo.AvailableFreeSpace;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    cpuUsage = GetCpuUsageWindows();
                    memoryUsage = GetMemoryUsageWindows();
                }
                else
                {
                    cpuUsage = GetCpuUsageUnix();
                    memoryUsage = GetMemoryUsageUnix();
                }
                _metrics.RequestDuration.WithLabels("ResourceUsage").Observe(timer.Elapsed.TotalSeconds);
                _metrics.CpuUsage.WithLabels(request.WorkerAddress).IncTo(cpuUsage);
                _metrics.MemoryUsage.WithLabels(request.WorkerAddress).IncTo(memoryUsage);
                _metrics.AvailableDiskSpace.WithLabels(request.WorkerAddress).IncTo(diskSpace);
                return Task.FromResult(new ResourceUsageResponse
                {
                    Status = true,
                    Message = "Successfully retrieved worker resource information.",
                    CpuUsage = cpuUsage,
                    MemoryUsage = memoryUsage,
                    DiskSpace = diskSpace
                });
            }
            catch (Exception ex)
            {
                _metrics.ErrorCount.WithLabels("ResourceUsage").Inc();
                _metrics.RequestDuration.WithLabels("ResourceUsage").Observe(timer.Elapsed.TotalSeconds);
                _metrics.CpuUsage.WithLabels(request.WorkerAddress).IncTo(0);
                _metrics.MemoryUsage.WithLabels(request.WorkerAddress).IncTo(0);
                _metrics.AvailableDiskSpace.WithLabels(request.WorkerAddress).IncTo(0);
                return Task.FromResult(new ResourceUsageResponse
                {
                    Status = false,
                    Message = $"Failed to retrieve resources: {ex}.",
                    CpuUsage = 0,
                    MemoryUsage = 0,
                    DiskSpace = 0
                });
            }
        }

        // HELPER FUNCTIONS FOR WINDOWS PLATFORMS
        private float GetCpuUsageWindows()
        {
            var output = RunCommand("wmic cpu get loadpercentage");
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1 && float.TryParse(lines[1].Trim(), out var cpuUsage))
            {
                return cpuUsage;
            }

            return 0;
        }

        private long GetMemoryUsageWindows()
        {
            var output = RunCommand("wmic OS get FreePhysicalMemory");
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length > 1 && long.TryParse(lines[1].Trim(), out var memoryUsage))
            {
                return memoryUsage * 1024;
            }

            return 0;
        }

        // HELPER FUNCTIONS FOR UNIX LIKE PLATFORMS
        private float GetCpuUsageUnix()
        {
            var output = RunCommand("top -bn1 | grep 'Cpu(s)' | sed 's/.*, *\\([0-9.]*\\)%* id.*/\\1/' | awk '{print 100 - $1}'");
            return float.TryParse(output, out var cpuUsage) ? cpuUsage : 0;
        }

        private long GetMemoryUsageUnix()
        {
            var output = RunCommand("free | grep Mem | awk '{print $3 * 1024}'");
            return long.TryParse(output, out var memoryUsage) ? memoryUsage : 0;
        }

        // Runs commands given the worker nodes unique operating system
        private string RunCommand(string command)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd" : "/bin/bash",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"/c {command}" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                using (var reader = process.StandardOutput)
                {
                    return reader.ReadToEnd().Trim();
                }
            }
        }
    }
}
