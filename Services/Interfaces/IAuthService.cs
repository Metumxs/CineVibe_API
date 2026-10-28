using CineVibe.API.DTOs.Auth;
using CineVibe.API.Models;

namespace CineVibe.API.Services.Interfaces
{
    public interface IAuthService
    {
        Task<User> RegisterAsync(RegisterRequestDto request);
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);

        // Generates a password reset token and sends it to the user's email
        Task ForgotPasswordAsync(ForgotPasswordRequestDto request);

        // Validates the reset token and updates the user's password
        Task ResetPasswordAsync(ResetPasswordRequestDto request);
    }
}