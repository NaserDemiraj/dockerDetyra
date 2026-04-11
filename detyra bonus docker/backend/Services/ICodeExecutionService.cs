using CodeLabAPI.DTOs;

namespace CodeLabAPI.Services
{
    public interface ICodeExecutionService
    {
        Task<CodeExecutionResponse> ExecuteCodeAsync(int userId, CodeExecutionRequest request);
    }
}
