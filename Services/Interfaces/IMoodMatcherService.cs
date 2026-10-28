using CineVibe.API.DTOs.Mood;
using CineVibe.API.DTOs.Movies;

namespace CineVibe.API.Services.Interfaces
{
    public interface IMoodMatcherService
    {
        // Returns all available mood tags for the tag selection UI.
        // Synchronous — no I/O, data is already resolved in singleton at startup.
        List<MoodTagDto> GetAllMoodTags();

        // Discovers movies matching the selected mood tags with a 3-level fallback cascade:
        //   Level 0 — full merged query (all constraints active).
        //   Level 1 — relaxed vote: VoteCountMin lowered, VoteCountMax and VoteAverageMin removed.
        //   Level 2 — genres + sort + dates only: all other constraints stripped.
        // Throws ArgumentException (400) for incompatible tag combinations.
        // Throws NotFoundException (404) if none of the provided tag IDs are valid.
        Task<PagedMovieSearchResultDto> GetMoviesByMoodTagsAsync(List<string> tagIds, int page);
    }
}