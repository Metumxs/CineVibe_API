using System.Text.Json.Serialization;

namespace CineVibe.API.DTOs.TMDB
{
    public class TmdbVideo
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("site")]
        public string Site { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty; // e.g., "Trailer", "Teaser", "Clip"
    }
}
