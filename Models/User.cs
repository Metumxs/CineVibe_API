using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CineVibe.API.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; } // Unique identifier for the MongoDB document

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty; // User's display name (not necessarily unique, required during registration)

        [BsonElement("email")]
        public string Email { get; set; } = null!; // User's email address (used for login)

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = null!; // Hashed password using BCrypt

        [BsonElement("watchedMovies")]
        public List<UserMovie> WatchedMovies { get; set; } = new();

        [BsonElement("watchlistMovies")]
        public List<UserMovie> WatchlistMovies { get; set; } = new();
    }
}