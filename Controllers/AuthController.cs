using CineVibe.API.DTOs.Auth;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CineVibe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Automatically maps to /api/auth
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        // Dependency injection of the authentication service
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST /api/auth/register
        // Exceptions (e.g. ConflictException) bubble up and are handled globally
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            var user = await _authService.RegisterAsync(request);

            return Ok(new
            {
                Message = "User registered successfully!",
                UserId = user.Id,
                Email = user.Email,
                Username = user.Username
            });
        }

        // POST /api/auth/login
        // UnauthorizedAccessException is caught by GlobalExceptionHandler -> 401
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            var response = await _authService.LoginAsync(request);

            return Ok(response);
        }

        // POST /api/auth/forgot-password
        // Returns 200 regardless of whether the email exists — intentional security measure.
        // We never reveal whether an account with a given email exists.
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        {
            await _authService.ForgotPasswordAsync(request);

            return Ok(new
            {
                message = "If an account with this email exists, a password reset link has been sent."
            });
        }

        // POST /api/auth/reset-password
        // NotFoundException thrown by service bubbles up to GlobalExceptionHandler -> 404
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
        {
            await _authService.ResetPasswordAsync(request);

            return Ok(new { message = "Password has been reset successfully." });
        }
    }
}