using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Mood
{
    // Query parameters for GET /api/mood/movies.
    // TagIds — comma-separated list of mood tag slugs (e.g. "dark-and-gritty,retro").
    // Parsed from a single query string parameter to keep the URL clean.
    // Max 4 tags per request (one per group: tone, era, scale, length).
    // Group and range compatibility is validated in MoodMatcherService.
    public class MoodMatcherQueryDto
    {
        [Required(ErrorMessage = "At least one mood tag is required.")]
        public string MoodTagIds { get; set; } = string.Empty;

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; set; } = 1;

        // Parsed in service — not exposed directly.
        public List<string> ParsedMoodTagIds =>
            MoodTagIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();
    }
}