using CineVibe.API.Configuration;
using CineVibe.API.DTOs.Mood;
using CineVibe.API.DTOs.Movies;
using CineVibe.API.DTOs.TMDB;
using CineVibe.API.Exceptions;
using CineVibe.API.Services.Interfaces;

namespace CineVibe.API.Services
{
    public class MoodMatcherService : IMoodMatcherService
    {
        // Minimum total_results from TMDB to consider a query successful.
        private const int FallbackThreshold = 20;

        // Maximum number of mood tags that can be combined in a single request.
        // Groups: tone, era, scale, length — one tag per group, up to 4 total.
        private const int MaxTagCount = 4;

        // Fallback sort used when no tag defines a sort preference.
        private const string DefaultSortBy = "vote_average.desc";

        // Applied as with_runtime.gte when no tag defines a minimum runtime.
        // Filters out short films and TV specials without requiring explicit tag configuration.
        private const int DefaultMinRuntimeMinutes = 60;

        private readonly List<ResolvedMoodTag> _resolvedMoodTags;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMovieService _movieService;
        private readonly ILogger<MoodMatcherService> _logger;

        public MoodMatcherService(
            List<ResolvedMoodTag> resolvedMoodTags,
            IHttpClientFactory httpClientFactory,
            IMovieService movieService,
            ILogger<MoodMatcherService> logger)
        {
            _resolvedMoodTags = resolvedMoodTags;
            _httpClientFactory = httpClientFactory;
            _movieService = movieService;
            _logger = logger;
        }

        public List<MoodTagDto> GetAllMoodTags()
        {
            return _resolvedMoodTags.Select(t => new MoodTagDto
            {
                Id = t.Id,
                Label = t.Label,
                Emoji = t.Emoji,
                Description = t.Description,
                GroupId = t.GroupId,
                VoteCountMin = t.VoteCountMin,
                VoteCountMax = t.VoteCountMax,
                RuntimeMinMinutes = t.RuntimeMinMinutes,
                RuntimeMaxMinutes = t.RuntimeMaxMinutes
            }).ToList();
        }

        public async Task<PagedMovieSearchResultDto> GetMoviesByMoodTagsAsync(
            List<string> tagIds,
            int page)
        {
            if (tagIds.Count > MaxTagCount)
                throw new ArgumentException($"Cannot combine more than {MaxTagCount} mood tags.");

            var selectedTags = tagIds
                .Select(id => _resolvedMoodTags.FirstOrDefault(t => t.Id == id))
                .Where(t => t != null)
                .Cast<ResolvedMoodTag>()
                .ToList();

            if (!selectedTags.Any())
                throw new NotFoundException("None of the provided mood tag IDs are valid.");

            // Guard: all provided IDs must resolve — catches typos and removed tags.
            if (selectedTags.Count != tagIds.Count)
                throw new NotFoundException(
                    $"One or more mood tag IDs are invalid: " +
                    $"{string.Join(", ", tagIds.Except(selectedTags.Select(t => t.Id)))}");

            // Guard: one tag per group — prevents e.g. two tone tags in one request.
            var uniqueGroupsCount = selectedTags.Select(t => t.GroupId).Distinct().Count();
            if (uniqueGroupsCount != selectedTags.Count)
                throw new ArgumentException("You cannot combine multiple tags from the same group.");

            return await ExecuteWithFallbackAsync(selectedTags, page);
        }

        // Merges parameters from all selected tags and executes a 3-level fallback cascade.
        //
        // Merging rules:
        //   GenreIds     — from tone tag only. Era, scale, length tags carry no genres.
        //   VoteCountMin — Math.Max() across all tags (strictest floor wins).
        //   VoteCountMax — Math.Min() across tags that define a cap. Guard: voteMin > voteMax throws.
        //   VoteAvgMin   — Math.Max() across tags that define it.
        //   SortBy       — scale > tone > era > length > default.
        //   RuntimeMin   — Math.Max() across all tags that define it.
        //   RuntimeMax   — Math.Min() across tags that define it. Guard: runtimeMin > runtimeMax throws.
        //   ReleaseDates — narrowest window (Max of minimums, Min of maximums). Guard: impossible range throws.
        //
        // Fallback cascade:
        //   Level 0 — full merged query (all constraints active).
        //   Level 1 — relaxed vote: VoteCountMin drops to individual tag minimum,
        //             VoteCountMax and VoteAverageMin removed.
        //   Level 2 — genres + sort + dates only: vote at 50, runtime removed.
        //             Release date constraints intentionally preserved —
        //             era constraints define the fundamental identity of the request.
        private async Task<PagedMovieSearchResultDto> ExecuteWithFallbackAsync(
            List<ResolvedMoodTag> tags,
            int page)
        {
            var merged = BuildMergedParams(tags);
            var tagIds = string.Join(", ", tags.Select(t => t.Id));

            // Level 0: full query.
            var level0 = await FetchDiscoverPageAsync(merged, page);
            if (level0?.TotalResultsCount >= FallbackThreshold)
            {
                _logger.LogInformation("MoodMatcher [{Tags}] resolved at level 0.", tagIds);
                return await MapAndCacheAsync(level0, page, fallbackLevel: 0);
            }

            // Level 1: relax vote constraints only.
            var level1Params = merged with
            {
                VoteCountMin = tags.Min(t => t.VoteCountMin),
                VoteCountMax = null,
                VoteAverageMin = null
            };
            var level1 = await FetchDiscoverPageAsync(level1Params, page);
            if (level1?.TotalResultsCount >= FallbackThreshold)
            {
                _logger.LogInformation("MoodMatcher [{Tags}] resolved at level 1 (relaxed vote).", tagIds);
                return await MapAndCacheAsync(level1, page, fallbackLevel: 1);
            }

            // Level 2: genres + sort + dates. Vote floor at 50, runtime removed.
            // Release dates intentionally preserved — era constraints define request identity.
            var level2Params = merged with
            {
                VoteCountMin = 50,
                VoteCountMax = null,
                VoteAverageMin = null,
                RuntimeMin = null,
                RuntimeMax = null
            };
            var level2 = await FetchDiscoverPageAsync(level2Params, page);
            if (level2?.TotalResultsCount > 0)
            {
                _logger.LogWarning("MoodMatcher [{Tags}] reached level 2 — all constraints dropped.", tagIds);
                return await MapAndCacheAsync(level2, page, fallbackLevel: 2);
            }

            _logger.LogWarning("MoodMatcher [{Tags}] fallback cascade exhausted.", tagIds);
            return new PagedMovieSearchResultDto { Page = page };
        }

        private MergedMoodParams BuildMergedParams(List<ResolvedMoodTag> tags)
        {
            var toneTag = tags.FirstOrDefault(t => t.GroupId == "tone");
            var scaleTag = tags.FirstOrDefault(t => t.GroupId == "scale");
            var eraTag = tags.FirstOrDefault(t => t.GroupId == "era");
            var lengthTag = tags.FirstOrDefault(t => t.GroupId == "length");

            // Genres come only from the tone tag.
            var genreIds = toneTag?.GenreIds ?? new List<int>();

            // SortBy priority: scale > tone > era > length > default.
            var sortBy = scaleTag?.SortBy
                ?? toneTag?.SortBy
                ?? eraTag?.SortBy
                ?? lengthTag?.SortBy
                ?? DefaultSortBy;

            // VoteCountMin: strictest (highest) floor across all tags.
            var voteCountMin = tags.Max(t => t.VoteCountMin);

            // VoteCountMax: narrowest cap if any tag defines one.
            var tagsWithVoteMax = tags.Where(t => t.VoteCountMax.HasValue).ToList();
            int? voteCountMax = tagsWithVoteMax.Any()
                ? tagsWithVoteMax.Min(t => t.VoteCountMax!.Value)
                : null;

            if (voteCountMax.HasValue && voteCountMin > voteCountMax.Value)
                throw new ArgumentException(
                    "The selected mood tags produce an impossible vote count range and cannot be combined.");

            // VoteAverageMin: strictest quality floor across all tags that define one.
            var tagsWithAvgMin = tags.Where(t => t.VoteAverageMin.HasValue).ToList();
            double? voteAverageMin = tagsWithAvgMin.Any()
                ? tagsWithAvgMin.Max(t => t.VoteAverageMin!.Value)
                : null;

            // RuntimeMin: strictest floor across all tags that define one.
            var runtimeMin = tags
                .Select(t => t.RuntimeMinMinutes)
                .Where(r => r.HasValue)
                .Select(r => r!.Value)
                .DefaultIfEmpty(0)
                .Max() is int rm && rm > 0 ? rm : (int?)null;

            // RuntimeMax: narrowest ceiling across all tags that define one.
            var tagsWithRuntimeMax = tags.Where(t => t.RuntimeMaxMinutes.HasValue).ToList();
            int? runtimeMax = tagsWithRuntimeMax.Any()
                ? tagsWithRuntimeMax.Min(t => t.RuntimeMaxMinutes!.Value)
                : null;

            if (runtimeMin.HasValue && runtimeMax.HasValue && runtimeMin.Value > runtimeMax.Value)
                throw new ArgumentException(
                    "The selected mood tags require contradictory runtime ranges and cannot be combined.");

            // IMPORTANT: Configuration binder deserializes JSON null strings as "" not null.
            // Use IsNullOrEmpty instead of != null to correctly filter absent date constraints.
            var releaseDateMin = tags
                .Select(t => t.ReleaseDateMin)
                .Where(d => !string.IsNullOrEmpty(d))
                .DefaultIfEmpty(null)
                .Max();

            var releaseDateMax = tags
                .Select(t => t.ReleaseDateMax)
                .Where(d => !string.IsNullOrEmpty(d))
                .DefaultIfEmpty(null)
                .Min();

            if (releaseDateMin != null
                && releaseDateMax != null
                && string.Compare(releaseDateMin, releaseDateMax, StringComparison.Ordinal) > 0)
            {
                throw new ArgumentException(
                    "The selected mood tags require contradictory release date ranges and cannot be combined.");
            }

            return new MergedMoodParams(
                GenreIds: genreIds,
                SortBy: sortBy,
                VoteCountMin: voteCountMin,
                VoteCountMax: voteCountMax,
                VoteAverageMin: voteAverageMin,
                RuntimeMin: runtimeMin,
                RuntimeMax: runtimeMax,
                ReleaseDateMin: releaseDateMin,
                ReleaseDateMax: releaseDateMax
            );
        }

        private async Task<TmdbPagedResponse<TmdbMoviePreview>?> FetchDiscoverPageAsync(
            MergedMoodParams p,
            int page)
        {
            var client = _httpClientFactory.CreateClient("TmdbClient");

            var genreParam = p.GenreIds.Any()
                ? $"&with_genres={string.Join("|", p.GenreIds)}"
                : string.Empty;

            var voteMaxParam = p.VoteCountMax.HasValue
                ? $"&vote_count.lte={p.VoteCountMax.Value}"
                : string.Empty;

            var voteAvgParam = p.VoteAverageMin.HasValue
                ? $"&vote_average.gte={p.VoteAverageMin.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}"
                : string.Empty;

            // RuntimeMin: use tag-derived value if present, otherwise apply default floor
            // to filter out short films and TV specials across all queries.
            var runtimeMinParam = p.RuntimeMin.HasValue
                ? $"&with_runtime.gte={p.RuntimeMin.Value}"
                : $"&with_runtime.gte={DefaultMinRuntimeMinutes}";

            var runtimeMaxParam = p.RuntimeMax.HasValue
                ? $"&with_runtime.lte={p.RuntimeMax.Value}"
                : string.Empty;

            var releaseDateMinParam = !string.IsNullOrEmpty(p.ReleaseDateMin)
                ? $"&primary_release_date.gte={p.ReleaseDateMin}"
                : string.Empty;

            var releaseDateMaxParam = !string.IsNullOrEmpty(p.ReleaseDateMax)
                ? $"&primary_release_date.lte={p.ReleaseDateMax}"
                : string.Empty;

            var url =
                $"discover/movie?language=en-US" +
                $"&sort_by={p.SortBy}" +
                $"&vote_count.gte={p.VoteCountMin}" +
                $"{voteMaxParam}" +
                $"{voteAvgParam}" +
                $"{genreParam}" +
                $"{runtimeMinParam}" +
                $"{runtimeMaxParam}" +
                $"{releaseDateMinParam}" +
                $"{releaseDateMaxParam}" +
                $"&page={page}";

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "MoodMatcher TMDB discover failed. StatusCode: {StatusCode}",
                    response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<TmdbPagedResponse<TmdbMoviePreview>>();
        }

        private async Task<PagedMovieSearchResultDto> MapAndCacheAsync(
            TmdbPagedResponse<TmdbMoviePreview> tmdbResponse,
            int page,
            int fallbackLevel)
        {
            var moviePreviewDtos = await _movieService.GetMoviesByIdsAsync(
                tmdbResponse.Results
                    .DistinctBy(m => m.Id)
                    .Select(m => m.Id.ToString()));

            return new PagedMovieSearchResultDto
            {
                Results = moviePreviewDtos,
                Page = tmdbResponse.Page,
                TotalPages = tmdbResponse.TotalPages,
                TotalResultsCount = tmdbResponse.TotalResultsCount,
                FallbackLevel = fallbackLevel
            };
        }

        // Immutable parameter bag passed through the fallback cascade.
        // record with-expressions allow clean creation of relaxed variants
        // without mutating the original merged params.
        private record MergedMoodParams(
            List<int> GenreIds,
            string SortBy,
            int VoteCountMin,
            int? VoteCountMax,
            double? VoteAverageMin,
            int? RuntimeMin,
            int? RuntimeMax,
            string? ReleaseDateMin,
            string? ReleaseDateMax
        );
    }
}