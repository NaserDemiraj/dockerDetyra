using CodeLabAPI.DTOs;
using CodeLabAPI.Data;

namespace CodeLabAPI.Services
{
    public class CodeExecutionService : ICodeExecutionService
    {
        private readonly IDockerService _dockerService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CodeExecutionService> _logger;

        public CodeExecutionService(IDockerService dockerService, ApplicationDbContext context, ILogger<CodeExecutionService> logger)
        {
            _dockerService = dockerService;
            _context = context;
            _logger = logger;
        }

        public async Task<CodeExecutionResponse> ExecuteCodeAsync(int userId, CodeExecutionRequest request)
        {
            string? containerId = null;
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                if (user == null)
                    return new CodeExecutionResponse { Success = false, Error = "User not found" };

                containerId = await _dockerService.CreateUserContainerAsync(userId);

                var startTime = DateTime.UtcNow;
                var result = await _dockerService.ExecuteCodeInContainerAsync(
                    containerId,
                    request.Language,
                    request.Code);
                var executionTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                var errorType = GetErrorType(result.Error);
                var formattedError = string.IsNullOrWhiteSpace(result.Error)
                    ? null
                    : $"{errorType}: {result.Error}";

                return new CodeExecutionResponse
                {
                    Success = result.Success,
                    Output = result.Output,
                    Error = formattedError,
                    ErrorType = result.Success ? null : errorType,
                    ExecutionTimeMs = executionTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed");
                return new CodeExecutionResponse
                {
                    Success = false,
                    Error = "internal_error: Execution failed: " + ex.Message,
                    ErrorType = "internal_error"
                };
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(containerId))
                {
                    await _dockerService.DeleteUserContainerAsync(containerId);
                }
            }
        }

        private static string GetErrorType(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return "runtime_exception";
            }

            var normalized = error.ToLowerInvariant();
            if (normalized.Contains("memory_limit_exceeded") || normalized.Contains("memoryerror") || normalized.Contains("out of memory"))
                return "memory_limit_violation";
            if (normalized.Contains("syntaxerror") || normalized.Contains("indentationerror") || normalized.Contains("compilation error"))
                return "compilation_error";
            if (normalized.Contains("timeout") || normalized.Contains("infinite loop"))
                return "timeout";
            return "runtime_exception";
        }
    }
}
