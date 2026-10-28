using System.Text.Json.Serialization;

namespace CineVibe.API.DTOs.TMDB
{
    public class TmdbVideosResponse
    {
        [JsonPropertyName("results")]
        public List<TmdbVideo> Results { get; set; } = new();
    }
}
