using System.Text.Json.Serialization;

namespace CineVibe.API.DTOs.TMDB
{
    public class TmdbGenre
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
