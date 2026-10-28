using CineVibe.API.DTOs.Movies;

namespace CineVibe.API.DTOs.Users
{
    public class UserMovieResponseDto
    {
        public MoviePreviewDto MoviePreview { get; set; } = null!;
        public DateTime AddedAt { get; set; }
        public DateTime? DetailsOpenedAt { get; set; }
    }
}