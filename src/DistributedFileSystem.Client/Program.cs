using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using DistributedFileSystem.MasterNode;
using DistributedFileSystem.Client;

using System;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddGrpc();
        builder.Services.AddSingleton<MasterNode.MasterNodeClient>(provider =>
        {
            var channel = GrpcChannel.ForAddress("http://localhost:5001");
            return new MasterNode.MasterNodeClient(channel);
        });

        var app = builder.Build();

        var url = "https://localhost:5000";
        Console.WriteLine($"Client Node listening on {url}");

        await app.RunAsync();
    }
}
