namespace CineVibe.API.Configuration
{
    public class AppSettings
    {
        public string FrontendBaseUrl { get; set; } = null!;
        public int PasswordResetTokenExpirationMinutes { get; set; } = 60; // Default 60 minutes if not set
    }
}