using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
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

        public AuthService(ApplicationDbContext context, IConfiguration configuration, IDockerService dockerService)
        {
            _context = context;
            _configuration = configuration;
            _dockerService = dockerService;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            if (_context.Users.Any(u => u.Email == request.Email || u.Username == request.Username))
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

            // Create personal container for user
            var containerId = await _dockerService.CreateUserContainerAsync(user.Id);
            user.ContainerId = containerId;
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

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == request.Username);
            if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                return new AuthResponse { Success = false, Message = "Invalid credentials" };

            // Ensure the user has a running container (recreate if it was deleted)
            if (string.IsNullOrWhiteSpace(user.ContainerId))
            {
                user.ContainerId = await _dockerService.CreateUserContainerAsync(user.Id);
                await _context.SaveChangesAsync();
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
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null && !string.IsNullOrWhiteSpace(user.ContainerId))
            {
                await _dockerService.DeleteUserContainerAsync(user.ContainerId);
                user.ContainerId = null;
                await _context.SaveChangesAsync();
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
