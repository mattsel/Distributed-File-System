using Prometheus;

namespace DistributedFileSystem.MasterNode.Models
{
    public class MetricsCollector
    {
        public Counter GrpcCallsCounter { get; }
        public Counter ErrorCount { get; }
        public Histogram RequestDuration { get; }

        public MetricsCollector()
        {
            GrpcCallsCounter = Metrics.CreateCounter(
                "master_node_grpc_calls_total",
                "Total number of gRPC calls received by the master node",
                new CounterConfiguration { LabelNames = new[] { "request" } }
            );

            ErrorCount = Metrics.CreateCounter(
                "master_node_errors_total",
                "Total number of errors encountered by the master node",
                new CounterConfiguration { LabelNames = new[] { "request" } }
            );

            RequestDuration = Metrics.CreateHistogram(
                "master_node_request_duration_seconds",
                "Histogram of request durations for the master node in seconds",
                new HistogramConfiguration { LabelNames = new[] { "request" } }
            );
        }
    }
}
