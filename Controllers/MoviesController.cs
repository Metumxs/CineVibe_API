using CineVibe.API.DTOs.Movies;
using CineVibe.API.Services;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineVibe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")] // Standard API controller routing: /api/movies
    public class MoviesController : ControllerBase
    {
        private readonly IMovieService _movieService;
        private readonly IUserService _userService;

        public MoviesController(IMovieService movieService, IUserService userService)
        {
            _movieService = movieService;
            _userService = userService;
        }

        // GET: api/movies/trending
        // Public endpoint to retrieve trending movies for the home page catalog.
        // Supports optional filtering and sorting via query parameters.
        // No [Authorize] attribute here, as guests should be able to see the catalog.
        [HttpGet("trending")]
        public async Task<ActionResult<List<MoviePreviewDto>>> GetCatalog(
            [FromQuery] MovieCatalogQueryDto? query)
        {
            var movies = await _movieService.GetCatalogMoviesAsync(query);
            return Ok(movies);
        }

        // GET: api/movies/{id}
        // Public endpoint -— guests can view movie details.
        // For authenticated users -- we additionally mark the first open timestamp
        // on their UserMovie entry (if this movie is in their Watched or Watchlist).
        [HttpGet("{id}")]
        public async Task<ActionResult<MovieDetailsDto>> GetMovieDetails(int id)
        {
            var movieDetails = await _movieService.GetMovieDetailsAsync(id);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                // Fire-and-forget: does not block the response.
                // Errors are caught and logged inside this service method.
                _ = _userService.MarkUserOpenedOneMovieDetailsAsync(userId, id.ToString());
            }

            return Ok(movieDetails);
        }

        // GET: api/movies/search?query=inception&page=1
        // Public endpoint — no [Authorize] required, search is available to all users.
        // NotFoundException and other exceptions bubble up to GlobalExceptionHandler.
        [HttpGet("search")]
        public async Task<ActionResult<PagedMovieSearchResultDto>> SearchMovies(
            [FromQuery] MovieSearchQueryDto movieSearchQuery)
        {
            var results = await _movieService.SearchMoviesAsync(
                movieSearchQuery.Query,
                movieSearchQuery.Page);

            return Ok(results);
        }
    }
}