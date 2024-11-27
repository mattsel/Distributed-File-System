using DistributedFileSystem.WorkerNode.Models;
using DistributedFileSystem.WorkerNode.Services;
using Prometheus;

// <summary>
// This function will map the gRPC and build the web application.
// The port that this funciton will run on depends on the specified port
// in appsettings.json. For local testing, you must define it inside of appsettings.LocalDevelopment.json
// </summary>
class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<MetricsCollector>();

        var app = builder.Build();
        app.MapGrpcService<WorkerNodeService>();
        app.UseRouting();
        app.MapMetrics();

        if (args.Length > 0) { await app.RunAsync($"https://localhost:{args[0]}"); }
        await app.RunAsync($"https://localhost:5002");
    }
}