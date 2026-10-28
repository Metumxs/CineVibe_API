using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Auth
{
    public class ForgotPasswordRequestDto
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string Email { get; set; } = null!;
    }
}