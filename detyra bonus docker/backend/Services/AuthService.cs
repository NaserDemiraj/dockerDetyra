using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CodeLabAPI.Data;
using CodeLabAPI.DTOs;
using CodeLabAPI.Models;

namespace CodeLabAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IDockerService _dockerService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ApplicationDbContext context, IConfiguration configuration, IDockerService dockerService, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _dockerService = dockerService;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                if (await _context.Users.AnyAsync(u => u.Email == request.Email || u.Username == request.Username))
                    return new AuthResponse { Success = false, Message = "User already exists" };

                var passwordHash = HashPassword(request.Password);
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = passwordHash
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(user);
                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    User = new UserDto { Id = user.Id, Username = user.Username, Email = user.Email }
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Registration failed due to a server error.", ex);
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                return new AuthResponse { Success = false, Message = "Invalid credentials" };

            // Create a persistent Docker container for this user if not already created
            if (string.IsNullOrEmpty(user.ActiveContainerId))
            {
                try
                {
                    _logger.LogInformation("Creating Docker container for user {UserId}", user.Id);
                    var containerId = await _dockerService.CreateUserContainerAsync(user.Id);
                    _logger.LogInformation("Successfully created container {ContainerId} for user {UserId}", containerId, user.Id);
                    user.ActiveContainerId = containerId;
                    user.ContainerCreatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create execution container for user {UserId}", user.Id);
                    return new AuthResponse { Success = false, Message = $"Failed to create execution container: {ex.Message}" };
                }
            }
            else
            {
                _logger.LogInformation("User {UserId} already has container {ContainerId}", user.Id, user.ActiveContainerId);
            }

            var token = GenerateJwtToken(user);
            return new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                User = new UserDto { Id = user.Id, Username = user.Username, Email = user.Email }
            };
        }

        public async Task LogoutAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user != null && !string.IsNullOrEmpty(user.ActiveContainerId))
            {
                try
                {
                    // Delete the persistent container when user logs out
                    await _dockerService.DeleteUserContainerAsync(user.ActiveContainerId);
                    user.ActiveContainerId = null;
                    user.ContainerCreatedAt = null;
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log but don't throw - container cleanup is best effort
                }
            }
        }

        public string GenerateJwtToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(
                _configuration["JwtSettings:SecretKey"] ?? "your-super-secret-key-that-is-longer-than-32-characters-minimum"));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new System.Security.Claims.Claim("id", user.Id.ToString()),
                new System.Security.Claims.Claim("username", user.Username),
                new System.Security.Claims.Claim("email", user.Email)
            };

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var salt = new byte[16];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(salt);
                }
                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                var hash = pbkdf2.GetBytes(20);
                var hashBytes = new byte[36];
                Array.Copy(salt, 0, hashBytes, 0, 16);
                Array.Copy(hash, 0, hashBytes, 16, 20);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private static bool VerifyPassword(string password, string hash)
        {
            var hashBytes = Convert.FromBase64String(hash);
            var salt = new byte[16];
            Array.Copy(hashBytes, 0, salt, 0, 16);
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            var hash2 = pbkdf2.GetBytes(20);
            for (int i = 0; i < 20; i++)
                if (hashBytes[i + 16] != hash2[i])
                    return false;
            return true;
        }
    }
}
