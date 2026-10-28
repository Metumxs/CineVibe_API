using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Auth
{
    public class ResetPasswordRequestDto
    {
        [Required(ErrorMessage = "Token is required.")]
        public string Token { get; set; } = null!; // GUID from the password reset email link

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        public string NewPassword { get; set; } = null!;
    }
}