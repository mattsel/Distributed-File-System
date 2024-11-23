using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DistributedFileSystem.MasterNode;
using DistributedFileSystem.WorkerNode;
using System;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddGrpc();
        builder.Services.AddSingleton<WorkerNode.WorkerNodeClient>(provider =>
        {
            var channel = GrpcChannel.ForAddress("http://localhost:5002");
            return new WorkerNode.WorkerNodeClient(channel);
        });

        var app = builder.Build();

        app.MapGrpcService<MasterNodeService>();

        var url = "https://localhost:5001";
        Console.WriteLine($"Master Node listening on {url}");

        await app.RunAsync();
    }
}
