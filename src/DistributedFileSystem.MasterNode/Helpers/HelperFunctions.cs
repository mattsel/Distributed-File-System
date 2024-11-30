using Google.Protobuf;
using Grpc.Net.Client;
using System.Diagnostics;
using DistributedFileSystem.WorkerNode;

namespace DistributedFileSystem.MasterNode.Helpers
{
    public class HelperFunctions
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<MasterNodeService> _logger;

        public HelperFunctions(MongoDbService mongoDbService, ILogger<MasterNodeService> logger)
        {
            _mongoDbService = mongoDbService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // General function to run seperate processes
        public async Task RunProcess(string language, string address, string path, string action = "")
        {
            try
            {
                string args;
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string absoluteScriptPath = Path.GetFullPath(Path.Combine(baseDirectory, path));

                if (action == "CreateNode" || action == "DeleteNode")
                {
                    if (language == "powershell") { args = $"{absoluteScriptPath} -address {address} -action {action}"; }
                    else { args = $"{absoluteScriptPath} {address} {action}"; }
                }
                else
                {
                    string port = address.ToString().Split(":")[2];
                    args = $"{absoluteScriptPath} {port}";
                }

                _logger.LogInformation($"Base directory: {baseDirectory}");
                _logger.LogInformation($"Script path: {absoluteScriptPath}");
                _logger.LogInformation($"Absolute script path: {absoluteScriptPath}");
                _logger.LogInformation(args);

                if (!File.Exists(absoluteScriptPath)) { _logger.LogError("Script file not found at: " + absoluteScriptPath); return; }
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = language,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = new Process { StartInfo = processStartInfo };
                process.Start();

                if (action == "")
                {
                    await _mongoDbService.CreateNode(address);
                    int pid = process.Id;
                    await _mongoDbService.SetWorkerPid(address, pid);
                }
                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(output)) { _logger.LogInformation(output); }
                if (!string.IsNullOrEmpty(errors)) { _logger.LogError(errors); }
                process.WaitForExit();
            }
            catch (Exception ex) { _logger.LogError($"Failed to add scrape: {ex}"); }
        }

        // Splits file to chunks based on available workers and chunk size for accuracy
        public List<byte[]> SplitFileToChunks(byte[] fileBytes, int chunkSize)
        {
            var chunks = new List<byte[]>();
            for (int i = 0; i < fileBytes.Length; i += chunkSize)
            {
                int size = Math.Min(chunkSize, fileBytes.Length - i);
                var chunk = new byte[size];
                Array.Copy(fileBytes, i, chunk, 0, size);
                chunks.Add(chunk);
            }
            return chunks;
        }

        // Function will retrieve any worker that is not in working status
        public async Task<List<string>> GetAvailableWorkers()
        {
            var availableWorkers = await _mongoDbService.GetAllAvailableWorkers();
            return availableWorkers;
        }
    }
}
