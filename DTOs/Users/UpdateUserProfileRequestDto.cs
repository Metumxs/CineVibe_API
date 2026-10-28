using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Users
{
    public class UpdateUserProfileRequestDto
    {
        [Required(ErrorMessage = "Username is required.")]
        [StringLength(30, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 30 characters.")]
        [RegularExpression(@"^[a-zA-Z0-9_.-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, dots, and hyphens.")]
        public string Username { get; set; } = string.Empty;
    }
}
