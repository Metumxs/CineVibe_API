using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Movies
{
    public class MovieSearchQueryDto
    {
        [Required(ErrorMessage = "Search query cannot be empty.")]
        [MinLength(2, ErrorMessage = "Search query must be at least 2 characters.")]
        public string Query { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; set; } = 1;

        // Reserved for future filtering — not used yet
        public string? SortBy { get; set; }
        public List<string>? Genres { get; set; }
        // MoodTags - could be used for future mood-based filtering
    }
}