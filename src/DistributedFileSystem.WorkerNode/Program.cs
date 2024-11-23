using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using DistributedFileSystem.WorkerNode;
using System.Threading.Tasks;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();
        var app = builder.Build();

        app.MapGrpcService<WorkerNodeService>();

        var url = "http://localhost:5002";
        Console.WriteLine($"Worker Node listening on {url}");

        await app.RunAsync();
    }
}
