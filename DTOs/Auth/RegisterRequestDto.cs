using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for user registration requests
    /// </summary>
    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, dots, and hyphens.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long")]
        public string Password { get; set; } = null!;
    }
}