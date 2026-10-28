using CineVibe.API.Configuration;
using CineVibe.API.DTOs.Users;
using CineVibe.API.Exceptions;
using CineVibe.API.Models;
using CineVibe.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace CineVibe.API.Services
{
    public class UserService : IUserService
    {
        private readonly IMongoCollection<User> _usersCollection;
        private readonly IMongoCollection<CachedMoviePreview> _moviePreviewsCacheCollection;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IMongoDatabase database, 
            IOptions<MongoDbSettings> mongoOption,
            ILogger<UserService> logger
            )
        {
            _usersCollection = database.GetCollection<User>(mongoOption.Value.UsersCollectionName);
            _moviePreviewsCacheCollection = database.GetCollection<CachedMoviePreview>(mongoOption.Value.MoviePreviewsCollectionName);
            _logger = logger;
        }

        public async Task<UserProfileDto> GetProfileAsync(string userId)
        {
            var user = await _usersCollection.Find(u => u.Id == userId).FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException($"User with ID {userId} not found.");

            return new UserProfileDto
            {
                Id = user.Id!,
                Username = user.Username,
                Email = user.Email,
                WatchedMovies = user.WatchedMovies
                    .Select(m => new UserMovieDto
                    {
                        MovieId = m.MovieId,
                        AddedAt = m.AddedAt,
                        DetailsOpenedAt = m.DetailsOpenedAt
                    }).ToList(),
                WatchlistMovies = user.WatchlistMovies
                    .Select(m => new UserMovieDto
                    {
                        MovieId = m.MovieId,
                        AddedAt = m.AddedAt,
                        DetailsOpenedAt = m.DetailsOpenedAt
                    }).ToList()
            };
        }

        public async Task UpdateProfileAsync(string userId, UpdateUserProfileRequestDto request)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update.Set(u => u.Username, request.Username);
            await ExecuteOneUpdateAsync(userId, filter, update);
        }

        public async Task AddWatchedMovieAsync(string userId, string movieId)
        {
            // Check if the user already has this movie in their watchlist.
            // If so, carry over detailsOpenedAt — the user already explored this movie.
            var user = await _usersCollection
                .Find(u => u.Id == userId)
                .Project(u => new { u.WatchedMovies, u.WatchlistMovies })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException($"User with ID {userId} not found.");

            // Idempotent — movie already in watched
            if (user.WatchedMovies.Any(m => m.MovieId == movieId))
                return;

            var existingDetailsOpenedAt = user.WatchlistMovies
                .FirstOrDefault(m => m.MovieId == movieId)
                ?.DetailsOpenedAt;

            var pushFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var pushUpdate = Builders<User>.Update.Push(u => u.WatchedMovies,
                new UserMovie
                {
                    MovieId = movieId,
                    DetailsOpenedAt = existingDetailsOpenedAt
                });

            await _usersCollection.UpdateOneAsync(pushFilter, pushUpdate);
        }

        public async Task AddMovieToWatchlistAsync(string userId, string movieId)
        {
            // Check if the user already has this movie in their watched list.
            // If so, carry over detailsOpenedAt — the user already explored this movie.
            var user = await _usersCollection
                .Find(u => u.Id == userId)
                .Project(u => new { u.WatchlistMovies, u.WatchedMovies })
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException($"User with ID {userId} not found.");

            // Idempotent — movie already in watchlist
            if (user.WatchlistMovies.Any(m => m.MovieId == movieId))
                return;

            var existingDetailsOpenedAt = user.WatchedMovies
                .FirstOrDefault(m => m.MovieId == movieId)
                ?.DetailsOpenedAt;

            var pushFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var pushUpdate = Builders<User>.Update.Push(u => u.WatchlistMovies,
                new UserMovie
                {
                    MovieId = movieId,
                    DetailsOpenedAt = existingDetailsOpenedAt // null if not in watched, real value if was explored
                });

            await _usersCollection.UpdateOneAsync(pushFilter, pushUpdate);
        }

        public async Task RemoveMovieFromListAsync(string userId, string movieId, MovieListType listType)
        {
            var pullFilter = Builders<User>.Filter.Eq(u => u.Id, userId);

            var pullUpdate = listType == MovieListType.Watched
                ? Builders<User>.Update.PullFilter(u => u.WatchedMovies,
                    m => m.MovieId == movieId)
                : Builders<User>.Update.PullFilter(u => u.WatchlistMovies,
                    m => m.MovieId == movieId);

            await ExecuteOneUpdateAsync(userId, pullFilter, pullUpdate);
        }

        public async Task<List<UserMovieDto>> GetWatchedMoviesAsync(string userId)
        {
            var projection = Builders<User>.Projection
                .Include(u => u.WatchedMovies)
                .Exclude(u => u.Id);

            var result = await _usersCollection
                .Find(u => u.Id == userId)
                .Project<User>(projection)
                .FirstOrDefaultAsync();

            if (result == null)
                throw new NotFoundException($"User with ID {userId} not found.");

            return result.WatchedMovies.Select(m => new UserMovieDto
            {
                MovieId = m.MovieId,
                AddedAt = m.AddedAt,
                DetailsOpenedAt = m.DetailsOpenedAt
            }).ToList();
        }

        public async Task<List<UserMovieDto>> GetWatchlistAsync(string userId)
        {
            var projection = Builders<User>.Projection
                .Include(u => u.WatchlistMovies)
                .Exclude(u => u.Id);

            var result = await _usersCollection
                .Find(u => u.Id == userId)
                .Project<User>(projection)
                .FirstOrDefaultAsync();

            if (result == null)
                throw new NotFoundException($"User with ID {userId} not found.");

            return result.WatchlistMovies.Select(m => new UserMovieDto
            {
                MovieId = m.MovieId,
                AddedAt = m.AddedAt,
                DetailsOpenedAt = m.DetailsOpenedAt
            }).ToList();
        }

        public async Task<WatchlistStatsDto> GetWatchlistStatsAsync(string userId)
        {
            var (watchlist, runtimeInCacheLookup) = await GetWatchlistWithRuntimeInCacheLookupAsync(userId);
            var totalCount = watchlist.Count;

            var notOpenedByUserCount = watchlist.Count(m => m.DetailsOpenedAt == null);

            var nobodyExploredCount = watchlist.Count(m =>
                m.DetailsOpenedAt == null &&
                (!runtimeInCacheLookup.TryGetValue(m.MovieId, out var rt) || rt == 0));

            var realTotalRuntimeMinutes = watchlist
                .Select(m => runtimeInCacheLookup.TryGetValue(m.MovieId, out var rt) ? rt : 0)
                .Where(rt => rt > 0)
                .Sum();

            var displayedTotalRuntimeMinutes = watchlist
                .Where(m => m.DetailsOpenedAt != null) // Only sum runtime for movies the user personally explored, to avoid misleading totals
                .Select(m => runtimeInCacheLookup.TryGetValue(m.MovieId, out var rt) ? rt : 0)
                .Where(rt => rt > 0)
                .Sum();

            int optimizerBulkButchSize = GetOptimizerBulkBatchSize(totalCount);
            var candidatesCount = GetOptimizerCandidates(watchlist, runtimeInCacheLookup).Count();

            return new WatchlistStatsDto
            {
                TotalWatchlistMoviesCount = totalCount,
                NotOpenedMovieDetailsCount = notOpenedByUserCount,
                NobodyExploredMovieDetailsCount = nobodyExploredCount,
                RealTotalMoviesRuntimeMinutes = realTotalRuntimeMinutes,
                DisplayedTotalMoviesRuntimeMinutes = displayedTotalRuntimeMinutes,
                SuggestedForRemovalMoviesCount = Math.Min(optimizerBulkButchSize, candidatesCount)
            };
        }

        public async Task<List<UserMovieDto>> GetWatchlistOptimizerMoviesAsync(string userId)
        {
            var (watchlist, runtimeInCacheLookup) = await GetWatchlistWithRuntimeInCacheLookupAsync(userId);
            var totalCount = watchlist.Count;

            var optimizerBulkBatchSize = GetOptimizerBulkBatchSize(totalCount);

            return GetOptimizerCandidates(watchlist, runtimeInCacheLookup)
                .Take(optimizerBulkBatchSize)
                .ToList();
        }

        public async Task<int> BulkUpdateWatchlistAsync(string userId, WatchlistBulkUpdateRequestDto request)
        {
            var discardIds = request.Actions
                .Where(a => a.Action == WatchlistMovieAction.Discard)
                .Select(a => a.MovieId)
                .ToList();

            // Keep actions require no DB operation
            if (!discardIds.Any())
                return 0;

            var filter = Builders<User>.Filter.Eq(u => u.Id, userId);
            var update = Builders<User>.Update.PullFilter(
                u => u.WatchlistMovies,
                m => discardIds.Contains(m.MovieId));

            var result = await _usersCollection.UpdateOneAsync(filter, update);

            if (!result.IsAcknowledged || result.MatchedCount == 0)
                throw new NotFoundException($"User with ID {userId} not found.");

            // Returns the number of IDs submitted for removal, not the MongoDB-level modified count.
            // MongoDB PullFilter modifies one user document regardless of how many array elements were removed,
            // so ModifiedCount would always be 0 or 1 — not useful here.
            return discardIds.Count;
        }

        // Helper method: returns watchlist movies sorted by removal priority.
        // Priority 1: UnexploredArea — nobody opened, runtime unknown
        // Priority 2: User never opened, but runtime known via another user
        // Priority 3: Oldest added first
        private IEnumerable<UserMovieDto> GetOptimizerCandidates(
            List<UserMovieDto> watchlist,
            Dictionary<string, int> runtimeLookup)
        {
            return watchlist
                .OrderBy(m =>
                {
                    var hasRuntimeInCache = runtimeLookup.TryGetValue(m.MovieId, out var rt) && rt > 0;

                    if (m.DetailsOpenedAt == null && !hasRuntimeInCache) return 0; // Priority 1
                    if (m.DetailsOpenedAt == null) return 1;                // Priority 2
                    return 2;                                               // Priority 3
                })
                .ThenBy(m => m.AddedAt);
        }

        // Helper method: determines how many movies to suggest for removal based on total watchlist size.
        private int GetOptimizerBulkBatchSize(int totalCount)
        {
            return Math.Min((int)Math.Floor(totalCount * 0.2), 20);
        }

        // Helper method: fetch watchlist along with a runtime lookup for all movies in the watchlist.
        private async Task<(List<UserMovieDto> watchlist, Dictionary<string, int> runtimeLookup)> 
            GetWatchlistWithRuntimeInCacheLookupAsync(string userId)
        {
            var watchlist = await GetWatchlistAsync(userId);

            var movieIds = watchlist
                .Select(m => int.TryParse(m.MovieId, out var id) ? id : -1)
                .Where(id => id != -1)
                .ToList();

            var cachedPreviews = await _moviePreviewsCacheCollection
                .Find(m => movieIds.Contains(m.Id))
                .ToListAsync();

            var runtimeInCacheLookup = cachedPreviews
                .ToDictionary(m => m.Id.ToString(), m => m.RuntimeMinutes);

            return (watchlist, runtimeInCacheLookup);
        }

        // This method is designed to be called whenever a user opens the details page of a movie.
        public async Task MarkUserOpenedOneMovieDetailsAsync(string userId, string movieId)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Reliable elemMatch:
                // MovieId matches AND (DetailsOpenedAt == null OR DetailsOpenedAt field does not exist)
                var elemMatchCondition = Builders<UserMovie>.Filter.And(
                    Builders<UserMovie>.Filter.Eq(m => m.MovieId, movieId),
                    Builders<UserMovie>.Filter.Or(
                        Builders<UserMovie>.Filter.Eq(m => m.DetailsOpenedAt, null),
                        Builders<UserMovie>.Filter.Exists(m => m.DetailsOpenedAt, false)
                    )
                );

                var watchedFilter = Builders<User>.Filter.And(
                    Builders<User>.Filter.Eq(u => u.Id, userId),
                    Builders<User>.Filter.ElemMatch(u => u.WatchedMovies, elemMatchCondition)
                );

                var watchedUpdate = Builders<User>.Update
                    .Set("watchedMovies.$.detailsOpenedAt", now);

                var watchlistFilter = Builders<User>.Filter.And(
                    Builders<User>.Filter.Eq(u => u.Id, userId),
                    Builders<User>.Filter.ElemMatch(u => u.WatchlistMovies, elemMatchCondition)
                );

                var watchlistUpdate = Builders<User>.Update
                    .Set("watchlistMovies.$.detailsOpenedAt", now);

                // Run both updates in parallel
                var t1 = _usersCollection.UpdateOneAsync(watchedFilter, watchedUpdate);
                var t2 = _usersCollection.UpdateOneAsync(watchlistFilter, watchlistUpdate);

                await Task.WhenAll(t1, t2);
            }
            catch (Exception ex)
            {
                // Log the error but do not throw, as this is a non-critical
                _logger.LogWarning(ex,
                    "Failed to mark movie {MovieId} as opened for user {UserId}.", movieId, userId);
            }
        }

        // Helper method to execute updates and handle common error scenarios
        private async Task ExecuteOneUpdateAsync(
            string userId, 
            FilterDefinition<User> filter, 
            UpdateDefinition<User> update)
        {
            try
            {
                var result = await _usersCollection.UpdateOneAsync(filter, update);

                if (!result.IsAcknowledged || result.MatchedCount == 0)
                {
                    _logger.LogWarning("Update failed for user {UserId}. User not found.", userId);
                    throw new NotFoundException($"User with ID {userId} not found.");
                }
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing update for user {UserId}", userId);
                throw;
            }
        }
        
        public async Task<User?> GetByIdAsync(string userId)
        {
            return await _usersCollection 
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();
        }
    }
}