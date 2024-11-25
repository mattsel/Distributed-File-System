using Prometheus;

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
