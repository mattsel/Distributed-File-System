using DistributedFileSystem.MasterNode;
using Grpc.Net.Client;

// <summary>
// This is the entry point for the client to interact with the master node. This client is unable to interact with the worker nodes
// as it uses the master node to interact with the worker nodes in the most optimal way. 
// </summary>
class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();

        // Maps the master node's https address to send gRPC requests to
        builder.Services.AddSingleton<MasterNode.MasterNodeClient>(provider =>
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            return new MasterNode.MasterNodeClient(channel);
        });

        var app = builder.Build();

        // EACH OF THESE ARE ENDPOINTS THAT CAN BE USED TO TEST gRPC CALLS IN DEVELOPMENT
        // Ex. curl https://localhost:5001/CreateNode or curl https://locatlhost:5001/HandleFiles
        // THIS USES GET REQUESTS TO TRIGGER A SET RESPONSE FOR TESTING PURPOSES
        app.MapGet("/", async context =>
        {
            await context.Response.WriteAsync("Client Node is running.");
        });

        app.MapGet("/CreateNode", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.CreateNodeAsync(new CreateNodeRequest { WorkerAddress = "https://localhost:5002" });
            return Results.Ok(response.Message);
        });

        app.MapGet("/DeleteNode", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.DeleteNodeAsync(new DeleteNodeRequest { WorkerAddress = "https://localhost:5002" });
            return Results.Ok(response.Message);
        });

        app.MapGet("/HandleFiles", async (MasterNode.MasterNodeClient client) =>
        {
            var chunkData = new byte[] { 0x01, 0x02, 0x03 };
            var response = await client.HandleFilesAsync(new HandleFilesRequest
            {
                FileName = "example.txt",
                ChunkData = Google.Protobuf.ByteString.CopyFrom(chunkData),
                ChunkSize = chunkData.Length
            });
            return Results.Ok(response.Message);
        });

        app.MapGet("/ChunkLocations", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.ChunkLocationsAsync(new ChunkLocationsRequest { FileName = "example.txt" });
            return Results.Ok(response);
        });

        app.MapGet("/ListFiles", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.ListFilesAsync(new ListFilesRequest());
            return Results.Ok(response.FileName);
        });

        app.MapGet("/GetWorkerResources", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.GetWorkerResourcesAsync(new GetWorkerResourcesRequest { WorkerAddress = "worker1" });
            return Results.Ok(response);
        });

        var url = "https://localhost:5000";
        Console.WriteLine($"Client Node listening on {url}");

        await app.RunAsync();
    }
}
