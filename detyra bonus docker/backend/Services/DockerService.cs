using System.Diagnostics;
using System.Text;

namespace CodeLabAPI.Services
{
    public class DockerService : IDockerService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DockerService> _logger;

        public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> CreateUserContainerAsync(int userId)
        {
            try
            {
                var containerName = $"codelab-user-{userId}";
                var cmd = $"docker run -d --name {containerName} -m 512m --cpus 0.5 codelab-worker sleep 3600";
                
                var output = await RunDockerCommandAsync(cmd);
                _logger.LogInformation($"Created container for user {userId}: {output}");
                return output.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create container for user {userId}");
                throw;
            }
        }

        public async Task<(bool Success, string Output, string? Error)> ExecuteCodeInContainerAsync(
            string containerId, 
            string language, 
            string code)
        {
            try
            {
                var filePath = Path.Combine(Path.GetTempPath(), $"code_{Guid.NewGuid()}.{GetFileExtension(language)}");
                await File.WriteAllTextAsync(filePath, code);

                var containerPath = $"/tmp/{Path.GetFileName(filePath)}";
                var copyCmd = $"docker cp {filePath} {containerId}:{containerPath}";
                await RunDockerCommandAsync(copyCmd);

                var command = language.ToLower() == "python" 
                    ? $"python {containerPath}"
                    : $"dotnet {containerPath}";

                var runCmd = $"docker exec {containerId} {command}";
                var output = await RunDockerCommandAsync(runCmd);

                File.Delete(filePath);

                return (Success: true, Output: output, Error: null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed in container");
                return (Success: false, Output: "", Error: ex.Message);
            }
        }

        public async Task DeleteUserContainerAsync(string containerId)
        {
            try
            {
                await RunDockerCommandAsync($"docker rm -f {containerId}");
                _logger.LogInformation($"Deleted container: {containerId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete container {containerId}");
            }
        }

        private async Task<string> RunDockerCommandAsync(string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {command}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (!string.IsNullOrEmpty(error))
                        _logger.LogError($"Docker error: {error}");

                    return output;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute docker command");
                    throw;
                }
            });
        }

        private static string GetFileExtension(string language) => language.ToLower() switch
        {
            "python" => "py",
            "csharp" => "cs",
            _ => "txt"
        };
    }
}
