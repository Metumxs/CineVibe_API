using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CineVibe.API.Models
{
    public class Review
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string UserId { get; set; } = null!;

        public string MovieId { get; set; } = null!;

        public int Rating { get; set; }

        public string Comment { get; set; } = null!;

        public bool IsSpoiler { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<ReviewLike> Likes { get; set; } = new();

        public int ReportsCount { get; set; } = 0;

        public List<ReviewReply> Replies { get; set; } = new();

    }
}