using CineVibe.API.DTOs.TMDB;
using System.Text.Json.Serialization;

namespace CineVibe.API.Models.TMDB
{
    public class TmdbMovieDetails
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("overview")] // Description
        public string Overview { get; set; } = string.Empty;

        [JsonPropertyName("release_date")]
        public string ReleaseDate { get; set; } = string.Empty;

        [JsonPropertyName("vote_average")] // Average rating
        public double VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterImagePath { get; set; } = string.Empty;

        [JsonPropertyName("backdrop_path")] // Background image path
        public string BackdropImagePath { get; set; } = string.Empty;

        [JsonPropertyName("runtime")] // Duration in minutes
        public int Runtime { get; set; }

        [JsonPropertyName("tagline")] // A short catchphrase or slogan for the movie
        public string Tagline { get; set; } = string.Empty;

        [JsonPropertyName("status")] // Current status of the movie (e.g., "Released", "Post Production")
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("genres")] 
        public List<TmdbGenre> Genres { get; set; } = new();

        [JsonPropertyName("videos")] // A nested object that contains a list of videos (trailers, teasers, etc.) related to the movie.
        public TmdbVideosResponse? Videos { get; set; }
    }
}