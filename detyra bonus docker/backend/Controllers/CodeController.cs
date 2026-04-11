using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using CodeLabAPI.DTOs;
using CodeLabAPI.Services;

namespace CodeLabAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CodeController : ControllerBase
    {
        private readonly ICodeExecutionService _executionService;
        private readonly ILogger<CodeController> _logger;

        public CodeController(ICodeExecutionService executionService, ILogger<CodeController> logger)
        {
            _executionService = executionService;
            _logger = logger;
        }

        [HttpPost("execute")]
        public async Task<ActionResult<CodeExecutionResponse>> ExecuteCode([FromBody] CodeExecutionRequest request)
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized();

            _logger.LogInformation($"User {userId} executing {request.Language} code");
            var response = await _executionService.ExecuteCodeAsync(userId, request);
            return Ok(response);
        }
    }
}
