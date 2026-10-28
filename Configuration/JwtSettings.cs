namespace CineVibe.API.Configuration
{
    public class JwtSettings
    {
        public string Key { get; set; } = null!;
        public string Issuer { get; set; } = null!;
        public string Audience { get; set; } = null!;
        public int TokenExpirationHours { get; set; } = 2; // fallback default
    }
}