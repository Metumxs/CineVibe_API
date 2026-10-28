namespace CineVibe.API.DTOs.Movies
{
    // The final data contract sent to the frontend.
    // Contains strictly the properties required
    // for rendering a movie card on the homepage or in lists.
    public class MoviePreviewDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public double Rating { get; set; }
        public string ReleaseDate { get; set; } = string.Empty;
        public List<string> Genres { get; set; } = new();
        public int RuntimeMinutes { get; set; }
    }
}