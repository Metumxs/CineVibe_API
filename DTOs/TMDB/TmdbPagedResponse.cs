using System.Text.Json.Serialization;

namespace CineVibe.API.DTOs.TMDB
{
    // Wrapper for TMDB paginated response.
    // We only care about the 'results' array to extract the movies.
    public class TmdbPagedResponse<T>
    {
        [JsonPropertyName("results")]
        public List<T> Results { get; set; } = new();

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("total_pages")]
        public int TotalPages { get; set; }

        [JsonPropertyName("total_results")]
        public int TotalResultsCount { get; set; }
    }
}