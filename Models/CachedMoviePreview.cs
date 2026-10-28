using CineVibe.API.Services;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CineVibe.API.Models
{
    // Represents the database entity for our cache collection.
    // Isolated from external API changes and optimized for our specific needs.
    [BsonIgnoreExtraElements]
    public class CachedMoviePreview
    {
        // Using TMDB's integer ID directly as our document ID.
        // This makes lookups and relationships (like Watched arrays) easier.
        [BsonId]
        [BsonRepresentation(BsonType.Int32)]
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        // The fully constructed URL, ready for the frontend to use.
        public string PosterUrl { get; set; } = string.Empty;

        public double Rating { get; set; }

        public string ReleaseDate { get; set; } = string.Empty;

        // Genres are already mapped to their string representation (e.g., "Action").
        public List<string> Genres { get; set; } = new();

        // Movie duration in minutes.
        // Default is 0 — populated only when the movie detail page is visited.
        // Used by Time Left Tracker. See team notes on this design decision.
        public int RuntimeMinutes { get; set; } = 0;

        // TTL (Time To Live) indicator. 
        // Allows the service to determine if the cache is stale and needs a refresh.
        public DateTime CachedAt { get; set; }

        // Indicates the source of the cached data
        // (e.g., whether it was fetched from TMDB "get/popular" or from user interactions).
        public CacheSource Source { get; set; } = CacheSource.Opportunistic;

        // Rank by popularity within the catalog fetch (1 = most popular).
        // 0 = not a catalog entry (opportunistic). Used for default sort order.
        // Populated only during catalog refresh in GetCatalogMoviesAsync.
        public int CatalogPopularityRank { get; set; } = 0;
    }
}