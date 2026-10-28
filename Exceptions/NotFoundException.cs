namespace CineVibe.API.Exceptions
{
    // Thrown when a resource is not found (HTTP 404)
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}