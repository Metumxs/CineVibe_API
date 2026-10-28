using CineVibe.API.Configuration;
using CineVibe.API.DTOs.Dev;
using CineVibe.API.DTOs.TMDB;
using CineVibe.API.Models;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CineVibe.API.Controllers
{
    // DEV-ONLY UTILITY CONTROLLER :
    // Not intended for production use.
    // Used for one-time tasks like resolving TMDB keyword IDs.
    // Safe to delete after use, or keep behind environment check.
    [ApiController]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly IMongoCollection<CachedMoviePreview> _moviePreviewsCacheCollection;
        private readonly IMovieService _movieService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DevController> _logger;

        public DevController(
            IMongoDatabase database,
            IOptions<MongoDbSettings> mongoOptions,
            IMovieService movieService,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment env,
            ILogger<DevController> logger)
        {
            _moviePreviewsCacheCollection = database.GetCollection<CachedMoviePreview>(mongoOptions.Value.MoviePreviewsCollectionName);
            _movieService = movieService;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _logger = logger;
        }

        // GET /api/dev/keywords/resolve?names=heist,survival,grief
        // Resolves a comma-separated list of keyword names to TMDB keyword IDs.
        // Use results to populate KeywordIds in moodtags.json, then discard this endpoint.
        [HttpGet("keywords/resolve")]
        public async Task<IActionResult> ResolveKeywords([FromQuery] string names)
        {
            if (!_env.IsDevelopment())
                return NotFound();

            if (string.IsNullOrWhiteSpace(names))
                return BadRequest(new { message = "Query parameter 'names' is required." });

            var keywordNames = names
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct()
                .ToList();

            var client = _httpClientFactory.CreateClient("TmdbClient");

            // Resolve all keywords in parallel
            var tasks = keywordNames.Select(async name =>
            {
                var response = await client.GetAsync(
                    $"search/keyword?query={Uri.EscapeDataString(name)}&page=1");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "TMDB keyword search failed for '{Name}'. StatusCode: {StatusCode}",
                        name, response.StatusCode);

                    return new KeywordResolutionResultDto
                    {
                        Query = name,
                        Id = -1,
                        ResolvedName = null,
                        Found = false
                    };
                }

                var data = await response.Content
                    .ReadFromJsonAsync<TmdbPagedResponse<TmdbKeyword>>();

                // Take the first result — closest match to the query
                var match = data?.Results?.FirstOrDefault();

                return new KeywordResolutionResultDto
                {
                    Query = name,
                    Id = match?.Id ?? -1,
                    ResolvedName = match?.Name,
                    Found = match != null
                };
            });

            var results = await Task.WhenAll(tasks);

            return Ok(new
            {
                resolved = results.Where(r => r.Found).OrderBy(r => r.Query),
                notFound = results.Where(r => !r.Found).Select(r => r.Query)
            });
        }

        [HttpGet("cache/warm-up/movies-runtime")]
        public async Task<IActionResult> WarmUpCache()
        {
            if (!_env.IsDevelopment())
                return NotFound();

            var filter = Builders<CachedMoviePreview>.Filter.Eq(m => m.RuntimeMinutes, 0);

            var moviesToUpdate = await _moviePreviewsCacheCollection
                .Find(filter)
                .Project(m => m.Id)
                .ToListAsync();

            int total = moviesToUpdate.Count;
            int processed = 0;

            _logger.LogInformation("Starting cache warm-up for {Count} movies with zero runtime.", total);

            foreach (var movieId in moviesToUpdate)
            {
                try
                {
                    // 2. Метод GetMovieDetailsAsync отримає дані з TMDB та оновить документ у БД
                    // через PersistOpportunisticPreviewsInBackgroundAsync
                    await _movieService.GetMovieDetailsAsync(movieId);

                    processed++;
                    if (processed % 10 == 0)
                        _logger.LogInformation("Processed {Processed}/{Total} movies...", processed, total);

                    // IMPORTANT: Never change this delay
                    // to avoid TMDB API rate limits excess
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to warm up movie {Id}: {Message}", movieId, ex.Message);
                }
            }

            return Ok(new { message = "Warm-up completed", totalToUpdate = total, processed });
        }
    }
}