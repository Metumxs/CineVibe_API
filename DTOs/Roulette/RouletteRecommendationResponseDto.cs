using CineVibe.API.DTOs.Movies;

namespace CineVibe.API.DTOs.Roulette
{
    // Response model for POST /api/roulette/recommend.

    // MovieDetails — full movie details (the same MovieDetailsDto returned by GET /api/movies/{id}).
    // IsFallback + FallbackLevel — provide transparency for the Frontend (FE) :

    //   FallbackLevel 0 -> Exact match based on all selected criteria.
    //   FallbackLevel 1 -> Upper limit for vote_count removed.
    //   FallbackLevel 2 -> Only Q1 (Mood) genres used to broaden the search.
    //   FallbackLevel 3 -> Genres dropped completely, returning popular movies.
    //   FallbackLevel 4 -> Genres dropped, watched list ignored (extreme edge case).

    // The FE can display a message like "We slightly expanded the search for you" if IsFallback == true.
    public class RouletteRecommendationResponseDto
    {
        public MovieDetailsDto MovieDetails { get; set; } = null!;
        public bool IsFallback { get; set; }
        public int FallbackLevelCount { get; set; }
    }
}