using CineVibe.API.DTOs.Mood;
using CineVibe.API.DTOs.Movies;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CineVibe.API.Controllers
{
    [ApiController]
    [Route("api/mood")]
    public class MoodMatcherController : ControllerBase
    {
        private readonly IMoodMatcherService _moodMatcherService;

        public MoodMatcherController(IMoodMatcherService moodMatcherService)
        {
            _moodMatcherService = moodMatcherService;
        }

        // GET /api/mood/moodtags
        // Public endpoint — returns all available mood tags for the selection UI.
        // No [Authorize] required, same pattern as catalog and search.
        [HttpGet("moodtags")]
        public IActionResult GetAllTags()
        {
            var moodTags = _moodMatcherService.GetAllMoodTags();

            return Ok(moodTags);
        }

        // GET /api/mood/movies?tagIds=cozy-night-in,dark-and-gritty&page=1
        // Public endpoint — discovers movies matching selected mood tags.
        // Applies a fallback cascade internally if the strict intersection yields too few results.
        // NotFoundException bubbles up via GlobalExceptionHandler if tagIds are all invalid.
        [HttpGet("movies")]
        public async Task<ActionResult<PagedMovieSearchResultDto>> GetMoviesByMoodTags(
            [FromQuery] MoodMatcherQueryDto query)
        {
            var results = await _moodMatcherService.GetMoviesByMoodTagsAsync(
                query.ParsedMoodTagIds,
                query.Page);

            return Ok(results);
        }
    }
}