using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CineVibe.API.Models
{
    public class PasswordResetToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("userId")]
        public string UserId { get; set; } = null!; // Id of the user who requested the password reset

        [BsonElement("token")]
        public string Token { get; set; } = null!; // Unique token string for password reset

        [BsonElement("expiresAt")]
        public DateTime ExpiresAt { get; set; } // When the token expires and can no longer be used

        [BsonElement("isUsed")]
        public bool IsUsed { get; set; } = false; // Token can only be used once, so we track if it has been used
    }
}