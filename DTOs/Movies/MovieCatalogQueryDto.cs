namespace CineVibe.API.DTOs.Movies
{
    // Query parameters for GET /api/movies/trending.
    // All parameters are optional — omitting them returns the full catalog
    // sorted by popularity rank (default catalog order).
    public class MovieCatalogQueryDto
    {
        // Filter by genres — all specified genres must be present (AND logic).
        // Example: ?genres=Action&genres=Thriller
        public List<string>? Genres { get; set; }

        // Filter by minimum TMDB vote average (0.0 – 10.0).
        // Example: ?minRating=7.5
        public double? MinRating { get; set; }

        // Filter by release year range (inclusive).
        // Example: ?yearFrom=2000&yearTo=2020
        public int? YearFrom { get; set; }
        public int? YearTo { get; set; }

        // Sort order for the result set.
        // Supported values: "rating.desc", "rating.asc", "year.desc", "year.asc".
        // Default (null) — sorted by PopularityRank ascending (most popular first).
        public string? SortBy { get; set; }
    }
}