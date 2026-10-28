namespace CineVibe.API.DTOs.Users
{
    public class WatchlistStatsDto
    {
        // Total number of movies in the user's watchlist
        public int TotalWatchlistMoviesCount { get; set; }

        // Movies the user has never personally opened the detail page for
        public int NotOpenedMovieDetailsCount { get; set; }

        // Movies nobody has ever opened details page for (no info in cache, RuntimeMinutes == 0)
        public int NobodyExploredMovieDetailsCount { get; set; }

        // Total runtime of all watchlist movies where runtime is known (> 0, including movies user never opened details page for)
        public int RealTotalMoviesRuntimeMinutes { get; set; }

        // Total runtime of only the movies the user personally opened details page for
        public int DisplayedTotalMoviesRuntimeMinutes { get; set; }

        // Number of movies that will be suggested for removal in the optimizer batch
        public int SuggestedForRemovalMoviesCount { get; set; }
    }
}