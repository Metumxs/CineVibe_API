namespace CineVibe.API.Models;

public class ReviewReply
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = null!;

    public string Text { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReplyDto
{
    public string Text { get; set; } = null!;
    public string Username { get; set; } = null!;
}
