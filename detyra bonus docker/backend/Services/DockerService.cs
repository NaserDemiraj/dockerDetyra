using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
            var containerName = $"codelab-user-{userId}";

            // Remove any existing container with this name (force remove)
            await RunShellAsync($"docker rm -f {containerName} 2>/dev/null || true");

            var memLimit = _configuration.GetValue<int>("DockerSettings:MemoryLimit", 512);
            var cpuLimit = _configuration.GetValue<double>("DockerSettings:CpuLimit", 0.5);
            var cpuStr = cpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);

            var (_, _, exitCode) = await RunShellAsync(
                $"docker run -d --name {containerName} -m {memLimit}m --cpus {cpuStr} codelab-worker sleep infinity");

            if (exitCode != 0)
            {
                _logger.LogError("Failed to create container {ContainerName} for user {UserId}", containerName, userId);
                throw new Exception($"Failed to create Docker container for user {userId}");
            }

            _logger.LogInformation("Created container {ContainerName} for user {UserId}", containerName, userId);
            return containerName;
        }

        public async Task<(bool Success, string Output, string? Error)> ExecuteCodeInContainerAsync(
            string containerName,
            string language,
            string code)
        {
            try
            {
                // Check if the container is currently running
                var (inspectOut, _, _) = await RunShellAsync(
                    $"docker inspect --format {{{{.State.Running}}}} {containerName} 2>/dev/null");
                if (inspectOut.Trim() != "true")
                {
                    return (false, "", "Container is not running. Please try again.");
                }

                var codeFileName = $"code_{Guid.NewGuid()}";
                var localPath = Path.Combine(Path.GetTempPath(), codeFileName);
                await File.WriteAllTextAsync(localPath, code);

                var containerPath = $"/tmp/{codeFileName}";
                var (_, cpErr, cpExit) = await RunShellAsync($"docker cp \"{localPath}\" {containerName}:{containerPath}");

                if (File.Exists(localPath))
                    File.Delete(localPath);

                if (cpExit != 0)
                    return (false, "", $"Failed to copy code to container: {cpErr}");

                var lang = language.ToLower();
                var (execOut, execErr, _) = await RunShellAsync(
                    $"docker exec {containerName} python3 /app/executor.py {lang} \"{containerPath}\"");

                // Parse JSON output produced by executor.py
                var jsonText = string.IsNullOrWhiteSpace(execOut) ? execErr : execOut;
                try
                {
                    var result = JsonSerializer.Deserialize<ExecutorResult>(jsonText.Trim());
                    if (result != null)
                        return (result.Success, result.Output ?? "", result.Error);
                }
                catch (JsonException)
                {
                    _logger.LogWarning("Could not parse executor output as JSON: {Output}", jsonText);
                }

                return (true, jsonText, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed in container {ContainerName}", containerName);
                return (false, "", ex.Message);
            }
        }

        public async Task DeleteUserContainerAsync(string containerName)
        {
            try
            {
                await RunShellAsync($"docker rm -f {containerName}");
                _logger.LogInformation("Deleted container: {ContainerName}", containerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete container {ContainerName}", containerName);
            }
        }

        private async Task<(string Output, string Error, int ExitCode)> RunShellAsync(string command)
        {
            return await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.ArgumentList.Add("-c");
                psi.ArgumentList.Add(command);

                var process = new Process { StartInfo = psi };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error))
                    _logger.LogDebug("Shell stderr: {Error}", error);

                return (output.TrimEnd(), error.TrimEnd(), process.ExitCode);
            });
        }

        private record ExecutorResult(
            [property: JsonPropertyName("success")] bool Success,
            [property: JsonPropertyName("output")] string? Output,
            [property: JsonPropertyName("error")] string? Error,
            [property: JsonPropertyName("execution_time")] int ExecutionTime,
            [property: JsonPropertyName("language")] string? Language
        );
    }
}
