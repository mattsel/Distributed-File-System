using DistributedFileSystem.MasterNode;
using Grpc.Net.Client;

class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddGrpc();

        builder.Services.AddSingleton<MasterNode.MasterNodeClient>(provider =>
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5001");
            return new MasterNode.MasterNodeClient(channel);
        });

        var app = builder.Build();

        app.MapGet("/", async context =>
        {
            await context.Response.WriteAsync("Client Node is running.");
        });

        app.MapGet("/CreateNode", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.CreateNodeAsync(new CreateNodeRequest { WorkerAddress = "https://localhost:5003" });
            return Results.Ok(response.Message);
        });

        app.MapGet("/DeleteNode", async (MasterNode.MasterNodeClient client) =>
        {
            var response = await client.DeleteNodeAsync(new DeleteNodeRequest { WorkerAddress = "https://localhost:5003" });
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
            var response = await client.GetWorkerResourcesAsync(new GetWorkerResourcesRequest { WorkerAddress = "https://localhost:5003" });
            return Results.Ok(response);
        });

        app.MapGet("/DistributeFile", async (MasterNode.MasterNodeClient client, HttpContext context) =>
        {
            var chunkData = new byte[] { 0x01, 0x02, 0x03 };
            var request = new DistributeFileRequest
            {
                FileName = "example.txt",
                FileData = Google.Protobuf.ByteString.CopyFrom(chunkData),
            };

            var response = await client.DistributeFileAsync(request);
            return Results.Ok(response.Message);
        });

        app.MapGet("/RetrieveFile", async (MasterNode.MasterNodeClient client, HttpContext context) =>
        {
            string fileName = "example.txt";
            var response = await client.RetrieveFileAsync(new RetrieveFileRequest { FileName = fileName });
            return Results.Ok(response);
        });

        app.MapGet("/DeleteFile", async (MasterNode.MasterNodeClient client, HttpContext context) =>
        {
            string fileName = "example.txt";
            var response = await client.DeleteFileAsync(new DeleteFileRequest { FileName = fileName });
            return Results.Ok(response.Message);
        });


        var url = "https://localhost:5000";
        Console.WriteLine($"Client Node listening on {url}");

        await app.RunAsync();
    }
}
