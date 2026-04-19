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
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                if (user == null)
                    return new CodeExecutionResponse { Success = false, Error = "User not found" };

                // Check if user has a valid container, recreate if missing
                if (string.IsNullOrWhiteSpace(user.ContainerId))
                {
                    user.ContainerId = await _dockerService.CreateUserContainerAsync(userId);
                    _context.SaveChanges();
                }

                var startTime = DateTime.UtcNow;
                var result = await _dockerService.ExecuteCodeInContainerAsync(
                    user.ContainerId,
                    request.Language,
                    request.Code);
                var executionTime = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

                // If container is dead, recreate it
                if (!result.Success && result.Error?.Contains("not running") == true)
                {
                    user.ContainerId = await _dockerService.CreateUserContainerAsync(userId);
                    _context.SaveChanges();
                    
                    // Retry execution
                    result = await _dockerService.ExecuteCodeInContainerAsync(
                        user.ContainerId,
                        request.Language,
                        request.Code);
                    executionTime += (int)(DateTime.UtcNow - DateTime.UtcNow.AddMilliseconds(-executionTime)).TotalMilliseconds;
                }

                return new CodeExecutionResponse
                {
                    Success = result.Success,
                    Output = result.Output,
                    Error = result.Error,
                    ExecutionTimeMs = executionTime
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code execution failed");
                return new CodeExecutionResponse
                {
                    Success = false,
                    Error = "Execution failed: " + ex.Message
                };
            }
        }
    }
}
