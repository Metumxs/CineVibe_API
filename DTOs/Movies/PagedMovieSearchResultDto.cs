namespace CineVibe.API.DTOs.Movies
{
    public class PagedMovieSearchResultDto
    {
        public List<MoviePreviewDto> Results { get; set; } = new();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalResultsCount { get; set; }

        // Fallback cascade:
        //   Level 0 — full merged query (all constraints active).
        //   Level 1 — relaxed vote: VoteCountMin drops to individual tag minimum,
        //             VoteCountMax removed, VoteAverageMin removed.
        //   Level 2 — genres + sort + dates only: vote floor at 50, runtime removed.
        //             Release date constraints are always preserved — they define era identity.
        public int? FallbackLevel { get; set; }
    }
}