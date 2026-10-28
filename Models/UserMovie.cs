using MongoDB.Bson.Serialization.Attributes;

namespace CineVibe.API.Models
{
    // Represents a movie entry in a user's personal list.
    // Stores the movie ID and the date it was added,
    // enabling features like sorting, Time Left Tracker, and Mood Evolution.
    [BsonIgnoreExtraElements]
    public class UserMovie
    {
        [BsonElement("movieId")]
        public string MovieId { get; set; } = null!;

        [BsonElement("addedAt")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Tracks when the user first opened the movie detail page.
        // Null means the user has never personally visited this movie's detail page.
        // Used by Watchlist Optimizer stats to distinguish three states:
        //   1. DetailsOpenedAt != null                        -> user explored, runtime known
        //   2. DetailsOpenedAt == null + RuntimeMinutes > 0   -> runtime known via another user
        //   3. DetailsOpenedAt == null + RuntimeMinutes == 0  -> UnexploredArea: nobody opened it yet
        [BsonElement("detailsOpenedAt")]
        public DateTime? DetailsOpenedAt { get; set; } = null;
    }
}