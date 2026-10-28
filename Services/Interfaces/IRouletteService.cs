using CineVibe.API.DTOs.Roulette;

namespace CineVibe.API.Services.Interfaces
{
    public interface IRouletteService
    {
        // Returns a randomly selected set of three questions from the question banks.
        // Synchronous —- no I/O, just in-memory selection.
        RouletteQuestionsResponseDto GetQuestions();

        // Takes user answers, builds a TMDB query, filters out watched movies,
        // returns a recommended movie with fallback metadata.
        Task<RouletteRecommendationResponseDto> GetRecommendationAsync(
            RouletteRecommendationRequestDto request,
            string userId);
    }
}