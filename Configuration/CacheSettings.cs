namespace CineVibe.API.Configuration
{
    public class CacheSettings
    {
        public int MovieCatalogPreviewsCacheDurationHours { get; set; } = 24;
        public int MovieOpportunisticPreviewsCacheDurationHours { get; set; } = 24;
    }
}
