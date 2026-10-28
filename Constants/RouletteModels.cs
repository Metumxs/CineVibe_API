namespace CineVibe.API.Constants
{
    // Describes one roulette question with two answer options.
    public record RouletteQuestion(
        string Id,       // Unique identifier for the question (e.g., "mood_0", "genre_1")
        string CategoryID,
        string Text,     // Question text to display on the FE
        List<QuestionOption> AllOptions);

    // One answer option: label for FE + genre names (not IDs).
    // GenreNames are resolved to TMDB IDs via RouletteConstants.ResolveGenreIds().
    public record QuestionOption(
        string Id,
        string Label,
        RoulettePayload Payload);

    public record RoulettePayload(
        List<string>? GenreNames = null,
        string? Era = null,
        string? Language = null,
        int? MaxRuntime = null);

    // TMDB discover parameters for Q3 (niche level).
    public record NicheParams(string SortBy, int VoteCountMin, int VoteCountMax);

    // Combine GenreIds from Q1 and Q2, and NicheParams from Q3.
    // Passed through the fallback chain,
    // so record with-expression allows convenient creation
    // of variants with modified fields without mutations.
    public record TmdbRouletteDiscoverParams(
        List<int> GenreIds,
        string? Era,
        string? Language,
        int? MaxRuntime,
        NicheParams Niche);
}