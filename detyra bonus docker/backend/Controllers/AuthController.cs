using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CodeLabAPI.DTOs;
using CodeLabAPI.Services;

namespace CodeLabAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                if (!response.Success)
                    return BadRequest(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>();
                logger.LogError(ex, "Unhandled error during registration");
                return StatusCode(500, new AuthResponse { Success = false, Message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                if (!response.Success)
                    return Unauthorized(response);
                return Ok(response);
            }
            catch (Exception ex)
            {
                var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AuthController>>();
                logger.LogError(ex, "Unhandled error during login");
                return StatusCode(500, new AuthResponse { Success = false, Message = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            await _authService.LogoutAsync(userId);
            return Ok(new { Success = true, Message = "Logged out successfully" });
        }
    }
}
