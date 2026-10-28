namespace CineVibe.API.DTOs.Movies
{
    public class MovieDetailsDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Overview { get; set; } = string.Empty; // A brief summary of the movie's plot
        public string ReleaseDate { get; set; } = string.Empty; // Release date in "YYYY-MM-DD" format
        public double Rating { get; set; } // Average rating of the movie
        public int VoteCount { get; set; } // Number of votes the movie has received
        public string PosterUrl { get; set; } = string.Empty; // URL to the poster image
        public string BackdropUrl { get; set; } = string.Empty; // URL to the backdrop image
        public int RuntimeMinutes { get; set; }
        public string Tagline { get; set; } = string.Empty; // A short catchphrase or slogan for the movie
        public List<string> Genres { get; set; } = new();
        public bool IsReleased { get; set; }
        public string TrailerUrl { get; set; } = string.Empty; // URL to the trailer video (e.g., YouTube link)
    }
}