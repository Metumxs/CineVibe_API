using CineVibe.API.DTOs.Users;
using CineVibe.API.Models;

namespace CineVibe.API.Services.Interfaces
{
    public interface IUserService
    {
        // Methods below throw NotFoundException if user not found
        Task<UserProfileDto> GetProfileAsync(string userId);
        Task UpdateProfileAsync(string userId, UpdateUserProfileRequestDto request);
        Task AddWatchedMovieAsync(string userId, string movieId);
        Task AddMovieToWatchlistAsync(string userId, string movieId);
        Task RemoveMovieFromListAsync(string userId, string movieId, MovieListType listType);
        Task<List<UserMovieDto>> GetWatchedMoviesAsync(string userId);
        Task<List<UserMovieDto>> GetWatchlistAsync(string userId);
        Task MarkUserOpenedOneMovieDetailsAsync(string userId, string movieId);
        Task<WatchlistStatsDto> GetWatchlistStatsAsync(string userId);
        Task<List<UserMovieDto>> GetWatchlistOptimizerMoviesAsync(string userId);
        Task<int> BulkUpdateWatchlistAsync(string userId, WatchlistBulkUpdateRequestDto request);
        Task<User?> GetByIdAsync(string userId);
        

    }
}