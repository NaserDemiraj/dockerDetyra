namespace CodeLabAPI.Services
{
    public interface IDockerService
    {
        Task<string> CreateUserContainerAsync(int userId);
        Task<(bool Success, string Output, string? Error, string? ErrorType)> ExecuteCodeInContainerAsync(
            string containerId, 
            string language, 
            string code);
        Task DeleteUserContainerAsync(string containerId);
    }
}
