using System.Diagnostics;
using System.Text.Json;

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
                var containerName = $"codelab-exec-{userId}-{Guid.NewGuid():N}";

                var memLimit = _configuration.GetValue<int>("DockerSettings:MemoryLimit", 512);
                var cpuLimit = _configuration.GetValue<double>("DockerSettings:CpuLimit", 0.5);
                var cpuStr = cpuLimit.ToString(System.Globalization.CultureInfo.InvariantCulture);

                var cmd = $"docker run -d --name {containerName} -m {memLimit}m --cpus {cpuStr} --network none codelab-worker tail -f /dev/null";

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
                _logger.LogError(ex, "Failed to create container for user {UserId}", userId);
                throw;
            }
        }

        public async Task<(bool Success, string Output, string? Error, string? ErrorType)> ExecuteCodeInContainerAsync(
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
                    return (Success: false, Output: "", Error: "Container is not running. Please try again.", ErrorType: "runtime_exception");
                }

                var codeFile = $"code_{Guid.NewGuid()}.{GetFileExtension(language)}";
                tempFilePath = Path.Combine(Path.GetTempPath(), codeFile);
                await File.WriteAllTextAsync(tempFilePath, code);

                var containerPath = $"/tmp/{codeFile}";
                var copyResult = await RunDockerCommandAsync($"docker cp \"{tempFilePath}\" {containerId}:{containerPath}");
                if (copyResult.ExitCode != 0)
                {
                    return (Success: false, Output: "", Error: $"Failed to copy code to container: {copyResult.StdErr}", ErrorType: "runtime_exception");
                }

                var lang = language.ToLower();
                var executionResult = await RunDockerCommandAsync($"docker exec {containerId} python3 /app/executor.py {lang} \"{containerPath}\"");

                // Check if container was OOM-killed during execution
                var oomResult = await RunDockerCommandAsync($"docker inspect --format={{{{.State.OOMKilled}}}} {containerId}");
                var isOomKilled = oomResult.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                if (isOomKilled)
                {
                    return (Success: false, Output: "", Error: "Code exceeded the 512MB memory limit.", ErrorType: "memory_limit_violation");
                }

                if (executionResult.ExitCode != 0 && string.IsNullOrWhiteSpace(executionResult.StdOut))
                {
                    var errorText = string.IsNullOrWhiteSpace(executionResult.StdErr)
                        ? "Container execution failed."
                        : executionResult.StdErr;
                    return (Success: false, Output: "", Error: errorText, ErrorType: "runtime_exception");
                }

                var rawOutput = executionResult.StdOut.Trim();
                if (string.IsNullOrWhiteSpace(rawOutput))
                {
                    return (Success: false, Output: "", Error: "No response returned by execution worker.", ErrorType: "runtime_exception");
                }

                try
                {
                    var workerResponse = JsonSerializer.Deserialize<WorkerExecutionResponse>(rawOutput, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (workerResponse == null)
                    {
                        return (Success: false, Output: "", Error: "Invalid worker response.", ErrorType: "runtime_exception");
                    }

                    return (
                        Success: workerResponse.Success,
                        Output: workerResponse.Output ?? "",
                        Error: workerResponse.Error,
                        ErrorType: workerResponse.ErrorType
                    );
                }
                catch (JsonException)
                {
                    return (Success: false, Output: rawOutput, Error: "Invalid execution response format.", ErrorType: "runtime_exception");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed in container {ContainerId}", containerId);
                return (Success: false, Output: "", Error: ex.Message, ErrorType: "runtime_exception");
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
                return;

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
                _logger.LogError(ex, "Failed to delete container {ContainerId}", containerId);
            }
        }

        private async Task<DockerCommandResult> RunDockerCommandAsync(string command)
        {
            return await Task.Run(() =>
            {
                try
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
                    {
                        if (process.ExitCode != 0)
                            _logger.LogWarning("Docker command failed ({ExitCode}): {DockerError}", process.ExitCode, error);
                        else
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
            public string? ErrorType { get; set; }
        }
    }
}
