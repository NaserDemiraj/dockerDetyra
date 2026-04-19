using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CodeLabAPI.Services
{
    public class DockerService : IDockerService
    {
        private readonly ILogger<DockerService> _logger;

        public DockerService(IConfiguration configuration, ILogger<DockerService> logger)
        {
            _logger = logger;
        }

        public async Task<string> CreateUserContainerAsync(int userId)
        {
            try
            {
                var containerName = $"codelab-user-{userId}-{Guid.NewGuid():N}";
                var cmd = $"docker run -d --name {containerName} -m 512m --cpus 0.5 --network none codelab-worker tail -f /dev/null";

                var result = await RunDockerCommandAsync(cmd);
                if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StdOut))
                {
                    var error = string.IsNullOrWhiteSpace(result.StdErr) ? "Unknown docker run error" : result.StdErr;
                    throw new InvalidOperationException($"Failed to create container: {error}");
                }

                var containerId = result.StdOut.Trim();
                _logger.LogInformation("Created execution container for user {UserId}: {ContainerId}", userId, containerId);
                return containerId;
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
            var tempFilePath = string.Empty;
            try
            {
                // Check if container is still running
                var containerStatus = await RunDockerCommandAsync($"docker ps --filter id={containerId} --format {{{{.State}}}}");
                if (string.IsNullOrWhiteSpace(containerStatus.StdOut))
                {
                    return (Success: false, Output: "", Error: "Container is not running. Please try again.");
                }

                var codeFile = $"code_{Guid.NewGuid()}.{GetFileExtension(language)}";
                tempFilePath = Path.Combine(Path.GetTempPath(), codeFile);
                await File.WriteAllTextAsync(tempFilePath, code);

                var containerPath = $"/tmp/{codeFile}";
                var copyCmd = $"docker cp \"{tempFilePath}\" {containerId}:{containerPath}";
                var copyResult = await RunDockerCommandAsync(copyCmd);
                if (copyResult.ExitCode != 0)
                {
                    return (Success: false, Output: "", Error: $"Failed to copy code to container: {copyResult.StdErr}");
                }

                var command = language.ToLower() == "python" 
                    ? $"python /app/executor.py python \"{containerPath}\""
                    : $"python /app/executor.py csharp \"{containerPath}\"";

                var runCmd = $"docker exec {containerId} {command}";
                var executionResult = await RunDockerCommandAsync(runCmd);
                var oomResult = await RunDockerCommandAsync($"docker inspect --format='{{{{.State.OOMKilled}}}}' {containerId}");
                var isOomKilled = oomResult.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

                if (isOomKilled)
                {
                    return (Success: false, Output: "", Error: "MEMORY_LIMIT_EXCEEDED: Code exceeded the 512MB memory limit.");
                }

                if (executionResult.ExitCode != 0 && string.IsNullOrWhiteSpace(executionResult.StdOut))
                {
                    var errorText = string.IsNullOrWhiteSpace(executionResult.StdErr)
                        ? "Container execution failed."
                        : executionResult.StdErr;
                    return (Success: false, Output: "", Error: errorText);
                }

                var rawOutput = executionResult.StdOut.Trim();
                if (string.IsNullOrWhiteSpace(rawOutput))
                {
                    return (Success: false, Output: "", Error: "No response returned by execution worker.");
                }

                try
                {
                    var workerResponse = JsonSerializer.Deserialize<WorkerExecutionResponse>(rawOutput, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (workerResponse == null)
                    {
                        return (Success: false, Output: "", Error: "Invalid worker response.");
                    }

                    return (
                        Success: workerResponse.Success,
                        Output: workerResponse.Output ?? "",
                        Error: workerResponse.Error
                    );
                }
                catch (JsonException)
                {
                    return (Success: false, Output: rawOutput, Error: "Invalid execution response format.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed in container");
                return (Success: false, Output: "", Error: ex.Message);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(tempFilePath) && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        public async Task DeleteUserContainerAsync(string containerId)
        {
            if (string.IsNullOrWhiteSpace(containerId))
            {
                return;
            }

            try
            {
                var result = await RunDockerCommandAsync($"docker rm -f {containerId}");
                if (result.ExitCode == 0)
                {
                    _logger.LogInformation("Deleted container: {ContainerId}", containerId);
                }
                else
                {
                    _logger.LogWarning("Failed to delete container {ContainerId}: {Error}", containerId, result.StdErr);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to delete container {containerId}");
            }
        }

        private async Task<DockerCommandResult> RunDockerCommandAsync(string command)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = isWindows ? "cmd.exe" : "/bin/bash",
                            Arguments = isWindows ? $"/c {command}" : $"-lc \"{command.Replace("\"", "\\\"")}\"",
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
                    {
                        _logger.LogDebug("Docker stderr: {DockerError}", error);
                    }

                    return new DockerCommandResult
                    {
                        ExitCode = process.ExitCode,
                        StdOut = output,
                        StdErr = error
                    };
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

        private class DockerCommandResult
        {
            public int ExitCode { get; set; }
            public string StdOut { get; set; } = string.Empty;
            public string StdErr { get; set; } = string.Empty;
        }

        private class WorkerExecutionResponse
        {
            public bool Success { get; set; }
            public string? Output { get; set; }
            public string? Error { get; set; }
        }
    }
}
