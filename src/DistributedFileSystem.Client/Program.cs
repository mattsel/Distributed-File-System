using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using Grpc.Net.Client;
using DistributedFileSystem.MasterNode;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Options for commands
        var workerAddressOption = new Option<string>(
            name: "--worker-address",
            description: "Address of the worker node"
        );

        var filePathOption = new Option<string>(
            name: "--file-path",
            description: "Path to the file"
        );

        var rootCommand = new RootCommand("Distributed File System Command Line Interface");

        // CreateNode Command
        var createNodeCommand = new Command("CreateNode", "Create a new node")
        {
            workerAddressOption
        };
        createNodeCommand.SetHandler(async (workerAddress) =>
        {
            var client = CreateGrpcClient();
            var response = await client.CreateNodeAsync(new CreateNodeRequest { WorkerAddress = workerAddress });
            Console.WriteLine(response.Message);
        }, workerAddressOption);
        rootCommand.AddCommand(createNodeCommand);

        // DeleteNode Command
        var deleteNodeCommand = new Command("DeleteNode", "Delete a node")
        {
            workerAddressOption
        };
        deleteNodeCommand.SetHandler(async (workerAddress) =>
        {
            var client = CreateGrpcClient();
            var response = await client.DeleteNodeAsync(new DeleteNodeRequest { WorkerAddress = workerAddress });
            Console.WriteLine(response.Message);
        }, workerAddressOption);
        rootCommand.AddCommand(deleteNodeCommand);

        // SingleStore Command
        var singleStoreCommand = new Command("SingleStore", "Store a file chunk")
        {
            filePathOption
        };
        singleStoreCommand.SetHandler(async (filePath) =>
        {
            var client = CreateGrpcClient();
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Error: The file does not exist.");
                return;
            }

            var chunkData = await File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);
            var response = await client.SingleStoreAsync(new SingleStoreRequest
            {
                FileName = fileName,
                ChunkData = Google.Protobuf.ByteString.CopyFrom(chunkData),
                ChunkSize = chunkData.Length
            });
            Console.WriteLine(response.Message);
        }, filePathOption);
        rootCommand.AddCommand(singleStoreCommand);

        // ChunkLocations Command
        var chunkLocationsCommand = new Command("ChunkLocations", "Get the chunk locations of a file")
        {
            filePathOption
        };
        chunkLocationsCommand.SetHandler(async (filePath) =>
        {
            var fileName = Path.GetFileName(filePath);
            var client = CreateGrpcClient();
            var response = await client.ChunkLocationsAsync(new ChunkLocationsRequest { FileName = fileName });
            Console.WriteLine(response);
        }, filePathOption);
        rootCommand.AddCommand(chunkLocationsCommand);

        // ListFiles Command
        var listFilesCommand = new Command("ListFiles", "List all files");
        listFilesCommand.SetHandler(async () =>
        {
            var client = CreateGrpcClient();
            var response = await client.ListFilesAsync(new ListFilesRequest());
            foreach (var fileName in response.FileName)
            {
                Console.WriteLine(fileName);
            }
        });
        rootCommand.AddCommand(listFilesCommand);

        // GetWorkerResources Command
        var getWorkerResourcesCommand = new Command("GetWorkerResources", "Get resources of a worker")
        {
            workerAddressOption
        };
        getWorkerResourcesCommand.SetHandler(async (workerAddress) =>
        {
            var client = CreateGrpcClient();
            var response = await client.GetWorkerResourcesAsync(new GetWorkerResourcesRequest { WorkerAddress = workerAddress });
            Console.WriteLine(response);
        }, workerAddressOption);
        rootCommand.AddCommand(getWorkerResourcesCommand);

        // DistributeFile Command
        var distributeFileCommand = new Command("DistributeFile", "Distribute a file")
        {
            filePathOption
        };
        distributeFileCommand.SetHandler(async (filePath) =>
        {
            var client = CreateGrpcClient();
            if (!File.Exists(filePath))
            {
                Console.WriteLine("Error: The file does not exist.");
                return;
            }

            var chunkData = await File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(filePath);
            var request = new DistributeFileRequest
            {
                FileName = fileName,
                FileData = Google.Protobuf.ByteString.CopyFrom(chunkData),
            };

            var response = await client.DistributeFileAsync(request);
            Console.WriteLine(response.Message);
        }, filePathOption);
        rootCommand.AddCommand(distributeFileCommand);

        // RetrieveFile Command
        var retrieveFileCommand = new Command("RetrieveFile", "Retrieve a file")
        {
            filePathOption
        };
        retrieveFileCommand.SetHandler(async (filePath) =>
        {
            var fileName = Path.GetFileName(filePath);
            var client = CreateGrpcClient();
            var response = await client.RetrieveFileAsync(new RetrieveFileRequest { FileName = fileName });
            Console.WriteLine(response);
        }, filePathOption);
        rootCommand.AddCommand(retrieveFileCommand);

        // DeleteFile Command
        var deleteFileCommand = new Command("DeleteFile", "Delete a file")
        {
            filePathOption
        };
        deleteFileCommand.SetHandler(async (filePath) =>
        {
            var fileName = Path.GetFileName(filePath);
            var client = CreateGrpcClient();
            var response = await client.DeleteFileAsync(new DeleteFileRequest { FileName = fileName });
            Console.WriteLine(response.Message);
        }, filePathOption);
        rootCommand.AddCommand(deleteFileCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static MasterNode.MasterNodeClient CreateGrpcClient()
    {
        var channel = GrpcChannel.ForAddress("https://localhost:5001");
        return new MasterNode.MasterNodeClient(channel);
    }
}