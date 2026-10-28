using CineVibe.API.Configuration;
using CineVibe.API.Constants;
using CineVibe.API.DTOs.Movies;
using CineVibe.API.DTOs.TMDB;
using CineVibe.API.Exceptions;
using CineVibe.API.Models;
using CineVibe.API.Models.TMDB;
using CineVibe.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CineVibe.API.Services
{
    public class MovieService : IMovieService
    {
        // Static semaphore to prevent Thundering Herd:
        // only one request refreshes the catalog cache at a time,
        // regardless of how many Scoped instances exist.
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        private readonly IMongoCollection<CachedMoviePreview> _moviePreviewsCacheCollection;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CacheSettings _cacheSettings;
        private readonly TmdbSettings _tmdbSettings;
        private readonly ILogger<MovieService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public MovieService(
            IMongoDatabase database,
            IOptions<MongoDbSettings> mongoOptions,
            IOptions<CacheSettings> cacheOptions,
            IOptions<TmdbSettings> tmdbOptions,
            ILogger<MovieService> logger,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory)
        {
            _moviePreviewsCacheCollection = database.GetCollection<CachedMoviePreview>(mongoOptions.Value.MoviePreviewsCollectionName);
            _httpClientFactory = httpClientFactory;
            _cacheSettings = cacheOptions.Value;
            _tmdbSettings = tmdbOptions.Value;
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public async Task<List<MoviePreviewDto>> GetCatalogMoviesAsync(MovieCatalogQueryDto? query)
        {
            // 1. Check the database for existing cache
            var cachedMoviePreviewsList = await _moviePreviewsCacheCollection
                .Find(m => m.Source == CacheSource.Catalog)
                .ToListAsync();

            // 2. Validate cache freshness (configured in appsettings, default 24 hours)
            if (IsCacheFresh(cachedMoviePreviewsList, _cacheSettings.MovieCatalogPreviewsCacheDurationHours))
                return ApplyQueryMapAndSort(cachedMoviePreviewsList, query);

            // 3. Cache is stale or empty. Acquire lock to prevent concurrent refreshes.
            await _cacheLock.WaitAsync();
            try
            {
                // Double-check: another request may have refreshed the cache while we were waiting.
                cachedMoviePreviewsList = await _moviePreviewsCacheCollection
                .Find(m => m.Source == CacheSource.Catalog)
                .ToListAsync();

                if (IsCacheFresh(cachedMoviePreviewsList, _cacheSettings.MovieCatalogPreviewsCacheDurationHours))
                    return ApplyQueryMapAndSort(cachedMoviePreviewsList, query);
                
                // 4. Fetch fresh data from TMDB.
                // Using the named client configured in Program.cs (already contains BaseUrl and Token).
                var client = _httpClientFactory.CreateClient("TmdbClient");
                var allCatalogTmdbMoviePreviewsList = new List<TmdbMoviePreview>();

                // TMDB paginates results, 20 per page. Fetching first 10 pages = +-200 movies.
                for (int i = 1; i <= TmdbConstants.PopularMoviesPagesCount; i++)
                {
                    var response = await client.GetAsync($"discover/movie?language=en-US&sort_by=popularity.desc&page={i}");

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("TMDB returned {StatusCode} on page {Page}. Stopping fetch.", response.StatusCode, i);
                        break;
                    }

                    var tmdbData = await response.Content.ReadFromJsonAsync<TmdbPagedResponse<TmdbMoviePreview>>();

                    if (tmdbData != null && tmdbData.Results.Any())
                    {
                        allCatalogTmdbMoviePreviewsList.AddRange(tmdbData.Results);
                    }
                }

                // FALLBACK: If TMDB is unreachable, return stale cache instead of an error
                if (!allCatalogTmdbMoviePreviewsList.Any())
                {
                    _logger.LogWarning("Failed to fetch any movies from TMDB. Returning stale cache.");
                    return ApplyQueryMapAndSort(cachedMoviePreviewsList, query);
                }
                // 5. Transform TMDB models to our cache models
                var newCatalogCachedMoviePreviewsList = allCatalogTmdbMoviePreviewsList
                    .DistinctBy(m => m.Id)
                    .Select(m => MapTmdbMovieToCacheModel(m, CacheSource.Catalog))
                    .ToList();

                // 6. Demote stale catalog entries to Opportunistic before refreshing.
                // Movies that are still popular will be promoted back to Catalog during upsert.
                // Movies that dropped out will naturally expire via MongoDB TTL.
                var demoteFilter = Builders<CachedMoviePreview>.Filter.Eq(m => m.Source, CacheSource.Catalog);
                var demoteUpdate = Builders<CachedMoviePreview>.Update.
                    Set(m => m.Source, CacheSource.Opportunistic)
                    .Set(m => m.CatalogPopularityRank, 0);
                await _moviePreviewsCacheCollection.UpdateManyAsync(demoteFilter, demoteUpdate);

                // 7. Bulk Upsert catalog movies to database in a single network request
                var bulkOps = new List<WriteModel<CachedMoviePreview>>();

                for (int i = 0; i < newCatalogCachedMoviePreviewsList.Count; i++)
                {
                    var preview = newCatalogCachedMoviePreviewsList[i];

                    preview.Source = CacheSource.Catalog;

                    var filter = Builders<CachedMoviePreview>.Filter.Eq(m => m.Id, preview.Id);
                    var update = BuildCachedMovieUpdateDefinition(preview, popularityRank: i + 1);

                    bulkOps.Add(new UpdateOneModel<CachedMoviePreview>(filter, update) { IsUpsert = true });
                }

                // Do all upserts in one go. MongoDB will handle inserts vs updates based on the filter.
                if (bulkOps.Any())
                {
                    await _moviePreviewsCacheCollection.BulkWriteAsync(bulkOps);
                }

                return ApplyQueryMapAndSort(newCatalogCachedMoviePreviewsList, query);
            }
            finally
            {
                // Always release the lock, even if an exception occurs
                _cacheLock.Release();
            }
        }

        public async Task<MovieDetailsDto> GetMovieDetailsAsync(int id)
        {
            var client = _httpClientFactory.CreateClient("TmdbClient");

            // Fetch movie details and videos in a single request via append_to_response
            var response = await client.GetAsync($"movie/{id}?language=en-US&append_to_response=videos");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch details for movie {Id}. TMDB returned {StatusCode}", id, response.StatusCode);
                throw new NotFoundException($"Movie with ID {id} not found.");
            }

            var movieDetails = await response.Content.ReadFromJsonAsync<TmdbMovieDetails>();

            if (movieDetails == null)
                throw new NotFoundException($"Movie with ID {id} not found.");

            // 1. Looking for a video of type "Trailer" on the "YouTube" platform.
            // 2. If none found (e.g., for older movies), take the first available YouTube video.
            var trailer = movieDetails.Videos?.Results?.FirstOrDefault(v =>
                              v.Site == TmdbConstants.VideoSite.YouTube &&
                              v.Type == TmdbConstants.VideoType.Trailer)
                       ?? movieDetails.Videos?.Results?.FirstOrDefault(v =>
                              v.Site == TmdbConstants.VideoSite.YouTube);

            var trailerUrl = trailer != null
                ? $"https://www.youtube.com/watch?v={trailer.Key}"
                : string.Empty;

            // Opportunistic caching — persist movie preview with runtime.
            // This is the only place where RuntimeMinutes gets populated in cache,
            // since discover/search endpoints don't return runtime.
            var newMoviePreview = MapTmdbMovieToCacheModel(movieDetails, CacheSource.Opportunistic);

            _ = PersistOpportunisticPreviewsInBackgroundAsync(new List<CachedMoviePreview> { newMoviePreview });

            return new MovieDetailsDto
            {
                Id = movieDetails.Id,
                Title = movieDetails.Title,
                Overview = movieDetails.Overview,
                ReleaseDate = movieDetails.ReleaseDate,
                Rating = Math.Round(movieDetails.VoteAverage, 1),
                VoteCount = movieDetails.VoteCount,
                PosterUrl = !string.IsNullOrEmpty(movieDetails.PosterImagePath)
                    ? $"{_tmdbSettings.PosterImageBaseUrl}{movieDetails.PosterImagePath}"
                    : string.Empty,
                BackdropUrl = !string.IsNullOrEmpty(movieDetails.BackdropImagePath)
                    ? $"{_tmdbSettings.BackdropImageBaseUrl}{movieDetails.BackdropImagePath}"
                    : string.Empty,
                RuntimeMinutes = movieDetails.Runtime,
                Tagline = movieDetails.Tagline,
                Genres = movieDetails.Genres.Select(g => g.Name).ToList(),
                IsReleased = movieDetails.Status.Equals(TmdbConstants.MovieStatus.Released, StringComparison.OrdinalIgnoreCase),
                TrailerUrl = trailerUrl
            };
        }

        public async Task<List<MoviePreviewDto>> GetMoviesByIdsAsync(IEnumerable<string> movieIds)
        {
            var ids = new List<int>();

            foreach (var id in movieIds)
            {
                if (int.TryParse(id, out var parsed))
                    ids.Add(parsed);
                else
                    _logger.LogWarning("Invalid movie ID format: '{Id}'. Skipping.", id);
            }

            if (!ids.Any())
                return new List<MoviePreviewDto>();

            // 1. Fetch all available previews from our local cache in a single query
            var cachedMoviePreviews = await _moviePreviewsCacheCollection
                .Find(m => ids.Contains(m.Id))
                .ToListAsync();

            var cachedIds = cachedMoviePreviews.Select(m => m.Id).ToHashSet();
            var missingIds = ids.Where(id => !cachedIds.Contains(id)).ToList();

            // 2. Fast exit: everything was in cache
            if (!missingIds.Any())
                return MapCachedMoviePreviewsToDto(cachedMoviePreviews);

            // 3. Fetch missing movies from TMDB in parallel.
            // Task.WhenAll is safe here: even 50 concurrent requests are well within
            // TMDB's rate limit of ~40 requests/second.
            var client = _httpClientFactory.CreateClient("TmdbClient");

            var fetchTasks = missingIds.Select(async id =>
            {
                var response = await client.GetAsync($"movie/{id}?language=en-US");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to fetch movie {Id} from TMDB. StatusCode: {StatusCode}", id, response.StatusCode);
                    return null;
                }
                return await response.Content.ReadFromJsonAsync<TmdbMovieDetails>();
            });

            var newTmdbMovieDetailsList = await Task.WhenAll(fetchTasks);

            // 4. Map and upsert fetched movies into cache (opportunistic caching)
            var newMoviePreviews = newTmdbMovieDetailsList
                .OfType<TmdbMovieDetails>()
                .Select(m => MapTmdbMovieToCacheModel(m, CacheSource.Opportunistic))
                .ToList();

            // Note: BuildCachedMovieUpsertWriteModels does not guard against
            // overwriting Catalog entries with Opportunistic source.
            // Race condition risk exists during catalog refresh but is negligible at current scale.
            if (newMoviePreviews.Any())
            {
                var bulkOps = BuildCachedMovieUpsertWriteModels(newMoviePreviews);

                await _moviePreviewsCacheCollection.BulkWriteAsync(bulkOps);
            }

            // 5. Merge cached and freshly fetched results
            return MapCachedMoviePreviewsToDto(cachedMoviePreviews.Concat(newMoviePreviews).ToList());
        }

        // Searches TMDB for movies matching the query.
        public async Task<PagedMovieSearchResultDto> SearchMoviesAsync(string query, int page)
        {
            var client = _httpClientFactory.CreateClient("TmdbClient");

            // Uri.EscapeDataString protects against special characters in the search query that could break the URL.
            var response = await client.GetAsync(
                $"search/movie?query={Uri.EscapeDataString(query)}&language=en-US&page={page}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "TMDB search failed for query '{Query}' page {Page}. StatusCode: {StatusCode}",
                    query, page, response.StatusCode);

                // Return an empty result with pagination metadata instead of throwing an exception
                // as search is a non-critical operation and it's better to show "no results found" than an error.
                return new PagedMovieSearchResultDto { Page = page };
            }

            var tmdbPagedResponseData = await response.Content
                .ReadFromJsonAsync<TmdbPagedResponse<TmdbMoviePreview>>();

            if (tmdbPagedResponseData == null || !tmdbPagedResponseData.Results.Any())
                return new PagedMovieSearchResultDto { Page = page };

            // Mapping TMDB models to our cache model.
            var moviePreviewsList = tmdbPagedResponseData.Results
                .DistinctBy(m => m.Id)
                .Select(m => MapTmdbMovieToCacheModel(m, CacheSource.Opportunistic))
                .ToList();

            // Using ScopeFactory to create a new scope for the background task,
            // ensuring we get a fresh instance of the database context
            // without risking conflicts with the current request's scope.
            // This is crucial for thread safety and avoiding issues with shared DbContext instances.
            _ = PersistOpportunisticPreviewsInBackgroundAsync(moviePreviewsList);

            // Return the search results to the user immediately, without waiting for the cache to update.
            return new PagedMovieSearchResultDto
            {
                Results = MapCachedMoviePreviewsToDto(moviePreviewsList),
                Page = tmdbPagedResponseData.Page,
                TotalPages = tmdbPagedResponseData.TotalPages,
                TotalResultsCount = tmdbPagedResponseData.TotalResultsCount
            };
        }

        // HELPER METHODS -----------------------------------------------------------------------

        // Helper method: Builds a list of WriteModels
        // for bulk upsert operations on CachedMoviePreview documents.
        private List<WriteModel<CachedMoviePreview>> BuildCachedMovieUpsertWriteModels(List<CachedMoviePreview> moviePreviews)
        {
            return moviePreviews.Select(preview =>
            {
                var filter = Builders<CachedMoviePreview>.Filter.Eq(m => m.Id, preview.Id);
                var update = BuildCachedMovieUpdateDefinition(preview);

                return (WriteModel<CachedMoviePreview>)
                    new UpdateOneModel<CachedMoviePreview>(filter, update) { IsUpsert = true };

            }).ToList();
        }

        // Helper method:
        // Applies filtering and sorting to catalog entries before mapping to DTOs.
        // Operating on CachedMoviePreview gives access to PopularityRank
        // without exposing it to the API contract.
        private List<MoviePreviewDto> ApplyQueryMapAndSort(
            List<CachedMoviePreview> movies,
            MovieCatalogQueryDto? query)
        {
            var result = movies.AsEnumerable();

            if (query != null)
            {
                // Genre filter: AND logic — all specified genres must be present.
                if (query.Genres != null && query.Genres.Any())
                {
                    result = result.Where(m =>
                        query.Genres.All(g =>
                            m.Genres.Contains(g, StringComparer.OrdinalIgnoreCase)));
                }

                // Rating filter: inclusive lower bound.
                if (query.MinRating.HasValue)
                    result = result.Where(m => m.Rating >= query.MinRating.Value);

                // Year range filter: ReleaseDate is stored as "YYYY" string.
                if (query.YearFrom.HasValue)
                {
                    result = result.Where(m =>
                        int.TryParse(m.ReleaseDate, out var year) && year >= query.YearFrom.Value);
                }

                if (query.YearTo.HasValue)
                {
                    result = result.Where(m =>
                        int.TryParse(m.ReleaseDate, out var year) && year <= query.YearTo.Value);
                }

                result = query.SortBy switch
                {
                    "rating.desc" => result.OrderByDescending(m => m.Rating),
                    "rating.asc" => result.OrderBy(m => m.Rating),
                    "year.desc" => result.OrderByDescending(m => m.ReleaseDate),
                    "year.asc" => result.OrderBy(m => m.ReleaseDate),
                    "popularity.desc" => result.OrderBy(m => m.CatalogPopularityRank == 0 ? int.MaxValue : m.CatalogPopularityRank),
                    "popularity.asc" => result.OrderByDescending(m => m.CatalogPopularityRank == 0 ? int.MinValue : m.CatalogPopularityRank),
                    _ => result.OrderBy(m => m.CatalogPopularityRank == 0 ? int.MaxValue : m.CatalogPopularityRank)
                };
            }
            else
            {
                // Default: popularity descending (most popular first).
                result = result.OrderBy(m => m.CatalogPopularityRank == 0 ? int.MaxValue : m.CatalogPopularityRank);
            }

            return result.Select(p => new MoviePreviewDto
            {
                Id = p.Id,
                Title = p.Title,
                PosterUrl = p.PosterUrl,
                Rating = p.Rating,
                ReleaseDate = p.ReleaseDate,
                Genres = p.Genres,
                RuntimeMinutes = p.RuntimeMinutes
            }).ToList();
        }

        // Helper method: Persists a list of movie previews in the background, with safeguards to avoid overwriting fresher Catalog entries.
        private async Task PersistOpportunisticPreviewsInBackgroundAsync(List<CachedMoviePreview> moviePreviewsList)
        {
            try
            {
                // 1. Create a new scope that is independent of the current request's scope of HTTP request.
                using var scope = _scopeFactory.CreateScope();

                // 2. Get a new instance of the database and collection within this background task's scope.
                var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
                var mongoOptions = scope.ServiceProvider.GetRequiredService<IOptions<MongoDbSettings>>();
                var cacheSettings = scope.ServiceProvider.GetRequiredService<IOptions<CacheSettings>>().Value;

                var collection = db.GetCollection<CachedMoviePreview>(mongoOptions.Value.MoviePreviewsCollectionName);

                // Persist each movie preview one by one, checking the existing cache entry to avoid overwriting Catalog entries with Opportunistic ones.
                foreach (var moviePreview in moviePreviewsList)
                {
                    var existingCachedMoviePreview = await collection.Find(m => m.Id == moviePreview.Id).FirstOrDefaultAsync();

                    if (existingCachedMoviePreview != null)
                    {
                        if (existingCachedMoviePreview.Source == CacheSource.Catalog)
                        {
                            // Don't replace whole entry, but update runtime using $max
                            var runtimeFilter = Builders<CachedMoviePreview>.Filter
                                .Eq(m => m.Id, moviePreview.Id);
                            var runtimeUpdate = Builders<CachedMoviePreview>.Update
                                .Max(m => m.RuntimeMinutes, moviePreview.RuntimeMinutes);

                            await collection.UpdateOneAsync(runtimeFilter, runtimeUpdate);

                            continue;
                        }

                        if (existingCachedMoviePreview.CachedAt > DateTime.UtcNow.AddHours(-cacheSettings.MovieOpportunisticPreviewsCacheDurationHours))
                            continue; // Opportunistic is still fresh — full replace not needed
                    }

                    var newCachedMoviePreviewUpsertFilter = Builders<CachedMoviePreview>.Filter.Eq(m => m.Id, moviePreview.Id);

                    var update = BuildCachedMovieUpdateDefinition(moviePreview);
                    await collection.UpdateOneAsync(
                        newCachedMoviePreviewUpsertFilter,
                        update,
                        new UpdateOptions { IsUpsert = true });
                }
            }
            catch (Exception ex)
            {
                // If db persistence fails, log the error but do not throw, as this is a non-critical background operation.
                _logger.LogWarning(ex, "Failed to persist previews in background.");
            }
        }

        // Helper method: Returns true if the cache exists and is within the configured TTL window
        private bool IsCacheFresh(List<CachedMoviePreview> cacheList, int cacheDurationHours)
        {
            var cacheEntry = cacheList.FirstOrDefault();
            return cacheEntry != null &&
                   cacheEntry.CachedAt > DateTime.UtcNow.AddHours(-cacheDurationHours);
        }

        // Helper method: OVERLOAD_1 : Maps TmdbMoviePreview (from Search/Catalog) to Cache model
        private CachedMoviePreview MapTmdbMovieToCacheModel(TmdbMoviePreview m, CacheSource source)
        {
            return new CachedMoviePreview
            {
                Id = m.Id,
                Title = m.Title,
                PosterUrl = !string.IsNullOrEmpty(m.PosterPath)
                    ? $"{_tmdbSettings.PosterImageBaseUrl}{m.PosterPath}"
                    : string.Empty,
                Rating = Math.Round(m.VoteAverage, 1),
                ReleaseDate = !string.IsNullOrEmpty(m.ReleaseDate) && m.ReleaseDate.Length >= 4
                    ? m.ReleaseDate.Substring(0, 4)
                    : string.Empty,
                Genres = m.GenreIds
                    .Select(id => TmdbConstants.GenreMap.GetValueOrDefault(id, "Unknown"))
                    .ToList(),
                RuntimeMinutes = 0, // Not available in preview lists
                CachedAt = DateTime.UtcNow,
                Source = source
            };
        }

        // Helper method: OVERLOAD_2 : Maps TmdbMovieDetails (from specific movie endpoints) to Cache model
        private CachedMoviePreview MapTmdbMovieToCacheModel(TmdbMovieDetails m, CacheSource source)
        {
            return new CachedMoviePreview
            {
                Id = m.Id,
                Title = m.Title,
                PosterUrl = !string.IsNullOrEmpty(m.PosterImagePath)
                    ? $"{_tmdbSettings.PosterImageBaseUrl}{m.PosterImagePath}"
                    : string.Empty,
                Rating = Math.Round(m.VoteAverage, 1),
                ReleaseDate = !string.IsNullOrEmpty(m.ReleaseDate) && m.ReleaseDate.Length >= 4
                    ? m.ReleaseDate.Substring(0, 4)
                    : string.Empty,
                Genres = m.Genres.Select(g => g.Name).ToList(),
                RuntimeMinutes = m.Runtime,
                CachedAt = DateTime.UtcNow,
                Source = source
            };
        }

        // Helper method: Transforms a list of models from the database into the list of DTOs for API responses.
        private List<MoviePreviewDto> MapCachedMoviePreviewsToDto(
            List<CachedMoviePreview> cachedMoviePreviewsList)
        {
            return cachedMoviePreviewsList.Select(p => new MoviePreviewDto
            {
                Id = p.Id,
                Title = p.Title,
                PosterUrl = p.PosterUrl,
                Rating = p.Rating,
                ReleaseDate = p.ReleaseDate,
                Genres = p.Genres,
                RuntimeMinutes = p.RuntimeMinutes
            }).ToList();
        }

        // Helper method: Builds an update definition for a cached movie preview.
        // Uses $max for RuntimeMinutes to never overwrite a known runtime with 0.
        private UpdateDefinition<CachedMoviePreview> BuildCachedMovieUpdateDefinition(
            CachedMoviePreview moviePreview,
            int? popularityRank = null)
        {
            var update = Builders<CachedMoviePreview>.Update
                .Set(m => m.Title, moviePreview.Title)
                .Set(m => m.PosterUrl, moviePreview.PosterUrl)
                .Set(m => m.Rating, moviePreview.Rating)
                .Set(m => m.ReleaseDate, moviePreview.ReleaseDate)
                .Set(m => m.Genres, moviePreview.Genres)
                .Set(m => m.Source, moviePreview.Source)
                .Set(m => m.CachedAt, moviePreview.CachedAt)
                .Max(m => m.RuntimeMinutes, moviePreview.RuntimeMinutes);

            if (popularityRank.HasValue)
                update = update.Set(m => m.CatalogPopularityRank, popularityRank.Value);

            return update;
        }
    }
}