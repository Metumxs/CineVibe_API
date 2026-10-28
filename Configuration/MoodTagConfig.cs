namespace CineVibe.API.Configuration
{
    // Represents a single mood tag with all parameters needed
    // to build a TMDB discover query.
    // Designed to mirror a future MongoDB document structure
    // for easy migration if an admin panel is added later.
    public class MoodTagConfig
    {
        // Unique slug used as identifier (e.g. "cozy-night-in")
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Id of group this tag belongs to.
        // Groups: "tone" (genre/vibe), "era" (release date), "scale" (popularity), "length" (runtime).
        // Only one tag per group is allowed per request.
        public string GroupId { get; set; } = string.Empty;

        // Human-readable genre names — resolved to IDs at startup.
        // Must match keys in TmdbConstants.GenreMap exactly.
        // Era and scale tags leave this empty — genres always come from the tone tag.
        public List<string> GenreNames { get; set; } = new();

        // KEYWORD FIELDS — not used in the current filtering algorithm.
        // Retained as reference artifacts for potential future use.
        // The current implementation relies solely on genre, date, runtime,
        // vote count, vote average, and sort parameters.
        public List<int> KeywordIds { get; set; } = new();
        public List<string> KeywordNames { get; set; } = new();

        // TMDB discover sort_by parameter (e.g. "vote_average.desc")
        public string SortBy { get; set; } = "popularity.desc";

        public int VoteCountMin { get; set; } = 100;

        // Null = no upper limit. Used for niche/cult tags to cap mainstream results.
        public int? VoteCountMax { get; set; } = null;

        // Minimum TMDB vote average (0.0 – 10.0). Null = no constraint.
        // Used to enforce quality floor on tags where rating matters more than popularity.
        public double? VoteAverageMin { get; set; } = null;

        public int? RuntimeMinMinutes { get; set; } = null;
        public int? RuntimeMaxMinutes { get; set; } = null;

        // Null = no date restriction
        public string? ReleaseDateMax { get; set; } = null;
        public string? ReleaseDateMin { get; set; } = null;
    }

    public class MoodTagsSettings
    {
        public List<MoodTagConfig> MoodTags { get; set; } = new();
    }

    // Resolved version — built once at startup, injected as singleton.
    // This is what the service actually uses.
    public class ResolvedMoodTag
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;

        // Empty for era/scale tags — genres always come from the tone tag.
        public List<int> GenreIds { get; set; } = new();

        // KEYWORD FIELD — not used in the current filtering algorithm.
        // Retained for potential future use. See MoodTagConfig for details.
        public List<int> KeywordIds { get; set; } = new();

        public string SortBy { get; set; } = "popularity.desc";
        public int VoteCountMin { get; set; } = 100;
        public int? VoteCountMax { get; set; } = null;
        public double? VoteAverageMin { get; set; } = null;
        public int? RuntimeMinMinutes { get; set; } = null;
        public int? RuntimeMaxMinutes { get; set; } = null;
        public string? ReleaseDateMin { get; set; } = null;
        public string? ReleaseDateMax { get; set; } = null;
    }
}