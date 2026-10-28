namespace CineVibe.API.Exceptions
{
    // Thrown when a resource already exists (HTTP 409)
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}