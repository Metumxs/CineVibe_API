namespace CineVibe.API.DTOs.Roulette
{
    // Answer to GET /api/roulette/questions.
    // The frontend receives a randomly selected set of three questions and renders them —
    // without any hardcoding on the client side.
    public class RouletteQuestionsResponseDto
    {
        public List<RouletteQuestionDto> Questions { get; set; } = new();
    }

    public class RouletteQuestionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<RouletteOptionDto> Options { get; set; } = new();
    }

    public class RouletteOptionDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}