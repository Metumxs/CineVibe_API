namespace CineVibe.API.Constants
{
    public static class TmdbConstants
    {
        // Number of pages to fetch for trending movies to populate the home page catalog.
        // Those movies are cached for {MoviePreviewsCacheDurationHours}(CacheSettings, default = 24) hours.
        // TMDB typically returns 20 results per page.
        // IMPORTANT: 10 is a reasonable number to get a good variety of movies without overwhelming the cache or the API limits.
        public static readonly int PopularMoviesPagesCount = 10;

        // Video site and type constants.
        // These are based on TMDB's API documentation and are unlikely to change frequently.
        public static class VideoSite
        {
            public const string YouTube = "YouTube";
            public const string Vimeo = "Vimeo";
        }

        // Video types as defined by TMDB.
        // These represent the kind of video content (e.g., trailers, teasers).
        public static class VideoType
        {
            public const string Trailer = "Trailer";
            public const string Teaser = "Teaser";
            public const string Clip = "Clip";
        }

        public static class MovieStatus
        {
            public const string Rumored = "Rumored";
            public const string Planned = "Planned";
            public const string InProduction = "In Production";
            public const string PostProduction = "Post Production";
            public const string Released = "Released";
            public const string Canceled = "Canceled";
        }

        // Static dictionary for genre mapping. 
        // These IDs are defined by TMDB and rarely change.
        public static readonly Dictionary<int, string> GenreMap = new()
        {
            { 28, "Action" }, { 12, "Adventure" }, { 16, "Animation" }, { 35, "Comedy" },
            { 80, "Crime" }, { 99, "Documentary" }, { 18, "Drama" }, { 10751, "Family" },
            { 14, "Fantasy" }, { 36, "History" }, { 27, "Horror" }, { 10402, "Music" },
            { 9648, "Mystery" }, { 10749, "Romance" }, { 878, "Science Fiction" }, { 10770, "TV Movie" },
            { 53, "Thriller" }, { 10752, "War" }, { 37, "Western" }
        };



        // Precompute the reverse mapping from genre name to ID for efficient lookups when resolving genre names to IDs.
        private static readonly Dictionary<string, int> _genreNameToId =
            GenreMap.ToDictionary(kv => kv.Value, kv => kv.Key);
        // Method to resolve genre names to their corresponding TMDB IDs.
        public static List<int> ResolveGenreIds(params string[] genreNames) =>
            genreNames
                .Select(name => _genreNameToId.TryGetValue(name, out var id) ? id : -1)
                .Where(id => id != -1)
                .ToList();
    }
}