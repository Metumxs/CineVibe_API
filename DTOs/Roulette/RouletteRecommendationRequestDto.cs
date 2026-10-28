using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Roulette
{
    // Body of the POST /api/roulette/recommend request.
    // The FE sends exactly three answers — one for each question,
    // IN THE SAME ORDER they were received from GET /questions.
    public class RouletteRecommendationRequestDto
    {
        [Required]
        [MinLength(3, ErrorMessage = "Exactly 3 answers are required.")]
        [MaxLength(3, ErrorMessage = "Exactly 3 answers are required.")]
        public List<RouletteAnswerDto> Answers { get; set; } = new();

        // Session-level exclusions: movie IDs the user skipped or marked as already watched
        // during the current roulette session.
        // Merged with the user's watched list before candidate filtering.
        // No server-side cap — FE is responsible for enforcing skip limits separately.
        public List<string> ExcludedMovieIds { get; set; } = new();
    }

    public class RouletteAnswerDto
    {
        [Required]
        public string QuestionId { get; set; } = null!;

        [Required]
        public string OptionId { get; set; } = null!;
    }
}