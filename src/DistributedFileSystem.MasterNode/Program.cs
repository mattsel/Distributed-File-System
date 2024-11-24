using DistributedFileSystem.MasterNode.Services;

// <summary>
// The master node is important as it is the sole communicator for the worker nodes.
// When a client makes a request, it is always to the master node, in which the master node,
// can handle the response using it's various workers. Depending on the network's size, there may
// be a need for multiple master nodes to help maintain extensive worker nodes.
// </summary>

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddGrpc();
        builder.Services.AddSingleton<MongoDbService>();

        var app = builder.Build();

        app.MapGrpcService<MasterNodeService>();

        await app.RunAsync();
    }
}
