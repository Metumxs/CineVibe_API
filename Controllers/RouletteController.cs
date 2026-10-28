using CineVibe.API.DTOs.Roulette;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CineVibe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Roulette endpoints require authentication, as they are personalized based on the user's watched list and preferences.
    public class RouletteController : ControllerBase
    {
        private readonly IRouletteService _rouletteService;

        public RouletteController(IRouletteService rouletteService)
        {
            _rouletteService = rouletteService;
        }

        // GET /api/roulette/questions
        // Returns random 3 questions for the current roulette session.
        // Synchronous call — no I/O, always fast result.
        [HttpGet("questions")]
        public IActionResult GetQuestions()
        {
            var questions = _rouletteService.GetQuestions();

            return Ok(questions);
        }

        // POST /api/roulette/recommend
        // Takes three answers, returns a recommended movie.
        // NotFoundException (if fallback cascade exhausted) -> 404 via GlobalExceptionHandler.
        [HttpPost("recommend")]
        public async Task<IActionResult> GetRecommendation(
            [FromBody] RouletteRecommendationRequestDto request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "Invalid token claims." });

            var result = await _rouletteService.GetRecommendationAsync(request, userId);

            return Ok(result);
        }
    }
}