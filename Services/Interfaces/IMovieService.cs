using CineVibe.API.DTOs.Movies;

namespace CineVibe.API.Services.Interfaces
{
    // Interface defining movie-related business logic.
    // Abstracts the underlying data source (TMDB vs MongoDB) from the controllers.
    public interface IMovieService
    {
        // Retrieves catalog movies with optional filtering and sorting applied in-memory (only 200 entries).
        // Cache-aside pattern with SemaphoreSlim thundering herd protection is preserved.
        Task<List<MoviePreviewDto>> GetCatalogMoviesAsync(MovieCatalogQueryDto? query = null);

        // Retrieves detailed information about a specific movie by its ID.
        Task<MovieDetailsDto> GetMovieDetailsAsync(int id);

        Task<List<MoviePreviewDto>> GetMoviesByIdsAsync(IEnumerable<string> movieIds);

        // Searches TMDB for movies matching the query.
        // Results are opportunistically cached — existing Catalog entries are never overwritten.
        Task<PagedMovieSearchResultDto> SearchMoviesAsync(string query, int page);
    }
}
