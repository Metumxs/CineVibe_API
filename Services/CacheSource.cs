namespace CineVibe.API.Services
{
    public enum CacheSource
    { 
        Catalog,      // 0 
        Opportunistic // 1 - IMPORTANT: NEVER CHANGE. USED IN MONGODB ATLAS FOR TTL - 7 DAYS  
    }
}
