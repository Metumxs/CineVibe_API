namespace CineVibe.API.DTOs.Users
{
    // DTO for a movie entry in a user's personal list.
    // Mirrors the UserMovie model but lives in the DTO layer — decoupled from the database model.
    public class UserMovieDto
    {
        public string MovieId { get; set; } = null!;
        public DateTime AddedAt { get; set; }
        public DateTime? DetailsOpenedAt { get; set; }
    }
}