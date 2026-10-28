using CineVibe.API.Configuration;
using CineVibe.API.Constants;
using CineVibe.API.DTOs.Movies;
using CineVibe.API.DTOs.Roulette;
using CineVibe.API.DTOs.TMDB;
using CineVibe.API.Exceptions;
using CineVibe.API.Models;
using CineVibe.API.Services.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CineVibe.API.Services
{
    public class RouletteService : IRouletteService
    {
        private readonly IMongoCollection<User> _usersCollection;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMovieService _movieService;
        private readonly ILogger<RouletteService> _logger;

        public RouletteService(
            IMongoDatabase database,
            IOptions<MongoDbSettings> mongoOptions,
            IHttpClientFactory httpClientFactory,
            IMovieService movieService,
            ILogger<RouletteService> logger)
        {
            _usersCollection = database.GetCollection<User>(mongoOptions.Value.UsersCollectionName);
            _movieService = movieService;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public RouletteQuestionsResponseDto GetQuestions()
        {
            var result = new RouletteQuestionsResponseDto();
            var allCategories = RouletteConstants.QuestionBanks;
            var selectedCategories = allCategories.OrderBy(x => Random.Shared.Next()).Take(3).ToList();

            foreach (var category in selectedCategories)
            {
                var randomQuestion = category[Random.Shared.Next(category.Count)];

                var randomizedOptions = randomQuestion.AllOptions
                    .OrderBy(x => Random.Shared.Next())
                    .Take(3)
                    .Select(o => new RouletteOptionDto
                    {
                        Id = o.Id,
                        Label = o.Label
                    })
                    .ToList();

                result.Questions.Add(new RouletteQuestionDto
                {
                    Id = randomQuestion.Id,
                    Text = randomQuestion.Text,
                    Options = randomizedOptions
                });
            }

            result.Questions = result.Questions.OrderBy(x => Random.Shared.Next()).ToList();
            return result;
        }

        public async Task<RouletteRecommendationResponseDto> GetRecommendationAsync(
            RouletteRecommendationRequestDto request,
            string userId)
        {
            var watchedMoviesIdsList = await _usersCollection
                .Find(u => u.Id == userId)
                .Project(u => u.WatchedMovies.Select(m => m.MovieId))
                .FirstOrDefaultAsync();

            var watchedAndExcludedMoviesIdsSet = watchedMoviesIdsList?.ToHashSet() ?? new HashSet<string>();

            // Merge session-level exclusions into the watched set.
            // This guarantees skipped/already-seen movies are never recommended again
            // within the same session, even if the DB write hasn't completed yet.
            foreach (var id in request.ExcludedMovieIds)
                watchedAndExcludedMoviesIdsSet.Add(id);

            var tmdbDiscoverParams = BuildParamsTmdbDiscoverRoulette(request.Answers);

            return await TryFindMovieWithFallbackAsync(tmdbDiscoverParams, watchedAndExcludedMoviesIdsSet);
        }

        private async Task<RouletteRecommendationResponseDto> TryFindMovieWithFallbackAsync(
            TmdbRouletteDiscoverParams tmdbParams,
            HashSet<string> watchedIds)
        {
            var result = await FetchAndFilterAsync(tmdbParams, watchedIds);
            if (result != null) return new() { MovieDetails = result, IsFallback = false, FallbackLevelCount = 0 };

            var level1Params = tmdbParams with { Niche = tmdbParams.Niche with { VoteCountMax = int.MaxValue } };
            result = await FetchAndFilterAsync(level1Params, watchedIds);
            if (result != null) return new() { MovieDetails = result, IsFallback = true, FallbackLevelCount = 1 };

            var level2Params = tmdbParams with { GenreIds = tmdbParams.GenreIds.Take(4).ToList() };
            result = await FetchAndFilterAsync(level2Params, watchedIds);
            if (result != null) return new() { MovieDetails = result, IsFallback = true, FallbackLevelCount = 2 };

            var level3Params = new TmdbRouletteDiscoverParams(
                new List<int>(), null, null, null, new NicheParams("popularity.desc", 500, int.MaxValue)
            );
            result = await FetchAndFilterAsync(level3Params, watchedIds);
            if (result != null) return new() { MovieDetails = result, IsFallback = true, FallbackLevelCount = 3 };

            result = await FetchAndFilterAsync(level3Params, new HashSet<string>());
            if (result != null) return new() { MovieDetails = result, IsFallback = true, FallbackLevelCount = 4 };

            throw new NotFoundException("Could not find a MovieDetails recommendation. Please try again.");
        }

        private async Task<MovieDetailsDto?> FetchAndFilterAsync(
            TmdbRouletteDiscoverParams tmdbParams,
            HashSet<string> watchedIds)
        {
            var tmdbClient = _httpClientFactory.CreateClient("TmdbClient");
            var todayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var genreParam = tmdbParams.GenreIds.Any() ? $"&with_genres={string.Join("|", tmdbParams.GenreIds)}" : string.Empty;
            var voteMaxParam = tmdbParams.Niche.VoteCountMax == int.MaxValue ? string.Empty : $"&vote_count.lte={tmdbParams.Niche.VoteCountMax}";

            var dateParams = $"&primary_release_date.lte={todayDate}";

            if (!string.IsNullOrEmpty(tmdbParams.Era) && tmdbParams.Era != "any")
            {
                dateParams = tmdbParams.Era switch
                {
                    "classic" => "&primary_release_date.lte=1969-12-31",
                    "1970s" => "&primary_release_date.gte=1970-01-01&primary_release_date.lte=1979-12-31",
                    "1980s" => "&primary_release_date.gte=1980-01-01&primary_release_date.lte=1989-12-31",
                    "1990s" => "&primary_release_date.gte=1990-01-01&primary_release_date.lte=1999-12-31",
                    "2000s" => "&primary_release_date.gte=2000-01-01&primary_release_date.lte=2009-12-31",
                    "2010s" => "&primary_release_date.gte=2010-01-01&primary_release_date.lte=2019-12-31",
                    "2020s" => $"&primary_release_date.gte=2020-01-01&primary_release_date.lte={todayDate}",
                    _ => $"&primary_release_date.lte={todayDate}"
                };
            }

            var langParam = !string.IsNullOrEmpty(tmdbParams.Language) ? $"&with_original_language={tmdbParams.Language}" : string.Empty;
            var runtimeParam = tmdbParams.MaxRuntime.HasValue ? $"&with_runtime.lte={tmdbParams.MaxRuntime.Value}" : string.Empty;

            var baseQuery =
                $"discover/movie?language=en-US" +
                $"&sort_by={tmdbParams.Niche.SortBy}" +
                $"&vote_count.gte={tmdbParams.Niche.VoteCountMin}" +
                $"{voteMaxParam}" +
                $"{genreParam}" +
                $"{dateParams}" +
                $"{langParam}" +
                $"{runtimeParam}";

            var firstResponse = await tmdbClient.GetAsync($"{baseQuery}&page=1");
            if (!firstResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("TMDB discover failed. StatusCode: {StatusCode}", firstResponse.StatusCode);
                return null;
            }

            var firstPageData = await firstResponse.Content.ReadFromJsonAsync<TmdbPagedResponse<TmdbMoviePreview>>();
            if (firstPageData == null || !firstPageData.Results.Any()) return null;

            var maxPageNumberCount = Math.Min(firstPageData.TotalPages, RouletteConstants.MaxRandomPageNumber);
            var triedPagesNumbersSet = new HashSet<int>();

            for (int attempt = 0; attempt < RouletteConstants.MaxRetryAttemptsCount; attempt++)
            {
                int currentPageNumber;
                do { currentPageNumber = Random.Shared.Next(1, maxPageNumberCount + 1); }
                while (triedPagesNumbersSet.Contains(currentPageNumber) && triedPagesNumbersSet.Count < maxPageNumberCount);
                triedPagesNumbersSet.Add(currentPageNumber);

                TmdbPagedResponse<TmdbMoviePreview>? pageData;
                if (currentPageNumber == 1)
                {
                    pageData = firstPageData;
                }
                else
                {
                    var response = await tmdbClient.GetAsync($"{baseQuery}&page={currentPageNumber}");
                    if (!response.IsSuccessStatusCode) continue;
                    pageData = await response.Content.ReadFromJsonAsync<TmdbPagedResponse<TmdbMoviePreview>>();
                }

                if (pageData == null) continue;

                var candidatesMovies = pageData.Results
                    .Where(m => !watchedIds.Contains(m.Id.ToString()))
                    .Where(m => !string.IsNullOrEmpty(m.PosterPath))
                    .ToList();

                if (!candidatesMovies.Any()) continue;

                var pickedMovie = candidatesMovies[Random.Shared.Next(candidatesMovies.Count)];

                try
                {
                    var movieDetails = await _movieService.GetMovieDetailsAsync(pickedMovie.Id);
                    if (movieDetails != null) return movieDetails;
                }
                catch (NotFoundException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch details for candidate movie {MovieId}. Retrying with another candidate...", pickedMovie.Id);
                    continue;
                }
            }
            return null;
        }

        private TmdbRouletteDiscoverParams BuildParamsTmdbDiscoverRoulette(List<RouletteAnswerDto> answers)
        {
            var allGenreNames = new List<string>();
            string? selectedEra = null;
            string? selectedLanguage = null;
            int? selectedMaxRuntime = null;

            foreach (var answer in answers)
            {
                var question = RouletteConstants.QuestionBanks
                    .SelectMany(bank => bank)
                    .FirstOrDefault(q => q.Id == answer.QuestionId);

                if (question == null) continue;

                var selectedOption = question.AllOptions.FirstOrDefault(o => o.Id == answer.OptionId);
                if (selectedOption == null) continue;

                if (selectedOption.Payload.GenreNames != null)
                    allGenreNames.AddRange(selectedOption.Payload.GenreNames);

                if (selectedOption.Payload.Era != null) selectedEra = selectedOption.Payload.Era;
                if (selectedOption.Payload.Language != null) selectedLanguage = selectedOption.Payload.Language;
                if (selectedOption.Payload.MaxRuntime != null) selectedMaxRuntime = selectedOption.Payload.MaxRuntime;
            }

            var allGenreIds = TmdbConstants.ResolveGenreIds(allGenreNames.Distinct().ToArray());

            var nicheParams = new NicheParams("popularity.desc", 100, int.MaxValue);

            return new TmdbRouletteDiscoverParams(allGenreIds, selectedEra, selectedLanguage, selectedMaxRuntime, nicheParams);
        }
    }
}