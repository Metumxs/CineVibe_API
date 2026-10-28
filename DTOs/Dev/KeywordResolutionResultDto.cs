namespace CineVibe.API.DTOs.Dev
{
    // DEV-ONLY : 
    // Response DTO for dev keyword resolution utility.
    // Each entry maps a keyword name to its TMDB ID (or -1 if not found).
    public class KeywordResolutionResultDto
    {
        public string Query { get; set; } = string.Empty;
        public int Id { get; set; }
        public string? ResolvedName { get; set; } // Actual name as returned by TMDB
        public bool Found { get; set; }
    }
}