namespace CineVibe.API.DTOs.Mood
{
    // Represents a single mood tag sent to the frontend.
    // Used by GET /api/mood/moodtags to populate the tag selection UI.
    //
    // VoteCountMin, VoteCountMax, RuntimeMinMinutes, RuntimeMaxMinutes are exposed
    // so the frontend can compute tag compatibility client-side and disable
    // incompatible combinations before sending a request.
    // Example: hidden-gem (VoteCountMax=600) is incompatible with crowd-favorite (VoteCountMin=3000).
    // Example: quick-watch (RuntimeMaxMinutes=100) is incompatible with epic-journey (RuntimeMinMinutes=110).
    public class MoodTagDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public int VoteCountMin { get; set; }
        public int? VoteCountMax { get; set; }
        public int? RuntimeMinMinutes { get; set; }
        public int? RuntimeMaxMinutes { get; set; }
    }
}