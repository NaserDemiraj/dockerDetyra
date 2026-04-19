using CodeLabAPI.DTOs;
using CodeLabAPI.Models;

namespace CodeLabAPI.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task LogoutAsync(int userId);
        string GenerateJwtToken(User user);
    }
}
