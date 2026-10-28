using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Users
{
    public class WatchlistBulkUpdateRequestDto
    {
        [Required]
        [MinLength(1, ErrorMessage = "At least one action is required.")]
        [MaxLength(20, ErrorMessage = "Cannot process more than 20 actions at once.")]
        public List<WatchlistMovieActionDto> Actions { get; set; } = new();
    }

    public class WatchlistMovieActionDto
    {
        [Required]
        public string MovieId { get; set; } = null!;

        [Required]
        public WatchlistMovieAction Action { get; set; }
    }

    public enum WatchlistMovieAction
    {
        Keep,
        Discard
    }
}