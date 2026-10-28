namespace CineVibe.API.DTOs.Users
{
    public class UserProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Full UserMovie objects —- frontend gets movieId and addedAt for sorting and Time Left Tracker
        public List<UserMovieDto> WatchedMovies { get; set; } = new();
        public List<UserMovieDto> WatchlistMovies { get; set; } = new();
    }
}