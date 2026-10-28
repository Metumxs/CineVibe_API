using System.Text.Json.Serialization;

namespace CineVibe.API.DTOs.TMDB
{
    // Represents the raw data from TMDB /movie/popular endpoint.
    // Maps snake_case JSON properties to standard C# PascalCase.
    public class TmdbMoviePreview
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; } = string.Empty;

        [JsonPropertyName("vote_average")]
        public double VoteAverage { get; set; }

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        // TMDB returns genre IDs as integers.
        // We will map these to actual string names later in the service.
        [JsonPropertyName("genre_ids")]
        public List<int> GenreIds { get; set; } = new();
    }
}