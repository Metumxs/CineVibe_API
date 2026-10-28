using System.ComponentModel.DataAnnotations;

namespace CineVibe.API.DTOs.Movies
{
    public class MovieSelectionDto
    {
        [Required(ErrorMessage = "MovieId cannot be empty.")]
        public string MovieId { get; set; } = null!;
    }
}