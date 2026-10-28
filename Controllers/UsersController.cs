using CineVibe.API.DTOs.Movies;
using CineVibe.API.DTOs.Users;
using CineVibe.API.Models;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineVibe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // JWT authentication required for all endpoints in this controller.

    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IMovieService _movieService;

        public UsersController(IUserService userService, IMovieService movieService)
        {
            _userService = userService;
            _movieService = movieService;
        }

        // GET users/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserIdOrThrow();

            var profile = await _userService.GetProfileAsync(userId);

            return Ok(profile);
        }

        // PUT users/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequestDto request)
        {
            var userId = GetUserIdOrThrow();

            await _userService.UpdateProfileAsync(userId, request);

            return Ok(new UpdateUserProfileResponseDto
            {
                Message = "Profile updated successfully.",
                Username = request.Username
            });
        }

        // POST users/watched
        [HttpPost("watched")]
        public async Task<IActionResult> AddWatchedMovie([FromBody] MovieSelectionDto movieSelection)
        {
            var userId = GetUserIdOrThrow();

            await _userService.AddWatchedMovieAsync(userId, movieSelection.MovieId);

            return Ok(new { message = "Movie added to watched list." });
        }

        // POST users/watchlist
        [HttpPost("watchlist")]
        public async Task<IActionResult> AddMovieToWatchlist([FromBody] MovieSelectionDto movieSelection)
        {
            var userId = GetUserIdOrThrow();

            await _userService.AddMovieToWatchlistAsync(userId, movieSelection.MovieId);

            return Ok(new { message = "Movie added to watchlist." });
        }

        // DELETE users/list/{listType}/{movieId}
        [HttpDelete("list/{listType}/{movieId}")]
        public async Task<IActionResult> RemoveMovieFromList(string listType, string movieId)
        {
            var userId = GetUserIdOrThrow();

            if (!Enum.TryParse<MovieListType>(listType, ignoreCase: true, out var parsedListType))
                return BadRequest(new { message = $"Invalid list type '{listType}'. Valid values: Watched, Watchlist." });

            await _userService.RemoveMovieFromListAsync(userId, movieId, parsedListType);

            return Ok(new { message = $"Movie removed from {parsedListType} list." });
        }

        // GET users//watched
        [HttpGet("watched")]
        public Task<IActionResult> GetWatchedMovies()
            => HandleGetUserMovieListAsync(_userService.GetWatchedMoviesAsync);

        [HttpGet("watchlist")]
        public Task<IActionResult> GetWatchlist()
            => HandleGetUserMovieListAsync(_userService.GetWatchlistAsync);

        // GET users/watchlist/stats
        [HttpGet("watchlist/stats")]
        public async Task<IActionResult> GetWatchlistStats()
        {
            var userId = GetUserIdOrThrow();

            var watchlistStats = await _userService.GetWatchlistStatsAsync(userId);

            return Ok(watchlistStats);
        }

        // GET users/watchlist/optimizer
        // Returns a prioritized batch of movies suggested for removal.
        // Batch size = floor(20% of watchlist), capped at 20.
        // Priority: UnexploredArea first, then user-never-opened, then oldest added.
        [HttpGet("watchlist/optimizer")]
        public async Task<IActionResult> GetWatchlistOptimizerMovies()
        {
            var userId = GetUserIdOrThrow();

            var optimizerCandidateMovies = await _userService.GetWatchlistOptimizerMoviesAsync(userId);

            if (!optimizerCandidateMovies.Any())
                return Ok(new List<UserMovieResponseDto>());

            var candidateMovieIdsList = optimizerCandidateMovies.Select(m => m.MovieId).ToList();
            var candidateMoviePreviewsList = await _movieService.GetMoviesByIdsAsync(candidateMovieIdsList);

            var response = optimizerCandidateMovies.Select(um =>
            {
                var moviePreview = candidateMoviePreviewsList.FirstOrDefault(p => p.Id.ToString() == um.MovieId);

                return new UserMovieResponseDto
                {
                    MoviePreview = moviePreview!,
                    AddedAt = um.AddedAt,
                    DetailsOpenedAt = um.DetailsOpenedAt
                };
            })
            .Where(x => x.MoviePreview != null)
            .ToList();

            return Ok(response);
        }

        // POST users/watchlist/bulk-update
        // Accepts a batch of keep/discard actions (min 1, max 20).
        // Only Discard actions result in DB writes — Keep requires no operation.
        [HttpPost("watchlist/bulk-update")]
        public async Task<IActionResult> BulkUpdateWatchlist(
            [FromBody] WatchlistBulkUpdateRequestDto request)
        {
            var userId = GetUserIdOrThrow();

            var removedCount = await _userService.BulkUpdateWatchlistAsync(userId, request);

            return Ok(new
            {
                message = "Watchlist updated successfully.",
                removedCount
            });
        }

        // --- HELPER METHODS :

        private async Task<IActionResult> HandleGetUserMovieListAsync(Func<string, Task<List<UserMovieDto>>> getUserMovieListFunc)
        {
            var userId = GetUserIdOrThrow();

            var userMoviesList = await getUserMovieListFunc(userId);

            if (!userMoviesList.Any())
                return Ok(new List<UserMovieResponseDto>());

            var movieIdsList = userMoviesList.Select(m => m.MovieId).ToList();
            var moviePreviewsList = await _movieService.GetMoviesByIdsAsync(movieIdsList);

            var response = userMoviesList.Select(um =>
            {
                var preview = moviePreviewsList.FirstOrDefault(p => p.Id.ToString() == um.MovieId);

                return new UserMovieResponseDto
                {
                    MoviePreview = preview!,
                    AddedAt = um.AddedAt,
                    DetailsOpenedAt = um.DetailsOpenedAt
                };
            })
            .Where(x => x.MoviePreview != null)
            .ToList();

            return Ok(response);
        }

        private string GetUserIdOrThrow()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Invalid token claims.");

            return userId;
        }
    }
}