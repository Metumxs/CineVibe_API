namespace CineVibe.API.Configuration
{
    public class MongoDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
        public string UsersCollectionName { get; set; } = string.Empty;
        public string MoviePreviewsCollectionName { get; set; } = string.Empty;
        public string PasswordResetTokensCollectionName { get; set; } = string.Empty;
    }
}