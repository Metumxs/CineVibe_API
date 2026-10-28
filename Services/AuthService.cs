using CineVibe.API.Configuration;
using CineVibe.API.DTOs.Auth;
using CineVibe.API.Exceptions;
using CineVibe.API.Models;
using CineVibe.API.Services.Interfaces;
using CineVibe.API.Templates;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CineVibe.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMongoCollection<User> _usersCollection;
        private readonly IMongoCollection<PasswordResetToken> _passwordResetTokensCollection;
        private readonly JwtSettings _jwtSettings;
        private readonly AppSettings _appSettings;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
       IMongoDatabase database,
       IOptions<JwtSettings> jwtOptions,
       IOptions<MongoDbSettings> mongoOptions,
       IOptions<AppSettings> appSettings,
       IEmailService emailService,
       ILogger<AuthService> logger)
        {
            _usersCollection = database.GetCollection<User>(
                mongoOptions.Value.UsersCollectionName);
            _passwordResetTokensCollection = database.GetCollection<PasswordResetToken>(
                mongoOptions.Value.PasswordResetTokensCollectionName);
            _jwtSettings = jwtOptions.Value;
            _appSettings = appSettings.Value;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<User> RegisterAsync(RegisterRequestDto request)
        {
            // 1. Check if a user with this email already exists
            var existingUser = await _usersCollection.Find(u => u.Email == request.Email).FirstOrDefaultAsync();

            if (existingUser != null)
            {
                throw new ConflictException("User with this email already exists.");
            }

            // 2. Hash the password using BCrypt
            string hashedPassword = HashPassword(request.Password);

            // 3. Create the new User entity
            var newUser = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = hashedPassword
                // WatchedMovieIds and WannaWatchMovieIds are automatically initialized as empty lists
            };

            // 4. Save the user to MongoDB
            await _usersCollection.InsertOneAsync(newUser);

            return newUser;
        }

        public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _usersCollection.Find(u => u.Email == request.Email).FirstOrDefaultAsync();

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            var token = GenerateJwtToken(user);

            // Build and return the login response with a fresh JWT token
            return new LoginResponseDto
            {
                Token = token,
                Email = user.Email,
                Username = user.Username ?? string.Empty // If the old users don't have a username, return an empty string
            };
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequestDto request)
        {
            // Find the user by email.
            var user = await _usersCollection
                .Find(u => u.Email == request.Email)
                .FirstOrDefaultAsync();

            // If user with the provided email doesn't exist, we don't throw an error.
            // This is a common security practice to prevent email enumeration attacks.
            if (user == null)
            {
                _logger.LogWarning(
                    "Password reset requested for non-existent email: {Email}",
                    request.Email);
                return;
            }

            // Invalidate all previous active tokens for this user.
            // If the user requested reset twice, only the last email should work.
            var invalidateFilter = Builders<PasswordResetToken>.Filter
                .Eq(t => t.UserId, user.Id);
            var invalidateUpdate = Builders<PasswordResetToken>.Update
                .Set(t => t.IsUsed, true);
            await _passwordResetTokensCollection
                .UpdateManyAsync(invalidateFilter, invalidateUpdate);

            // Generate a new unique token.
            var newToken = Guid.NewGuid().ToString();

            // Save the reset token in MongoDB
            var resetToken = new PasswordResetToken
            {
                UserId = user.Id!,
                Token = newToken,

                // Default expiration time is 60 minutes (can be configured in appsettings.json)
                ExpiresAt = DateTime.UtcNow.AddMinutes(_appSettings.PasswordResetTokenExpirationMinutes),

                IsUsed = false
            };

            await _passwordResetTokensCollection.InsertOneAsync(resetToken);

            var resetUrl = $"{_appSettings.FrontendBaseUrl}/reset-password?token={newToken}";
            var htmlContent = EmailTemplates.GetPasswordResetHtml(resetUrl);
            var subject = "CineVibe - Password Reset";

            await _emailService.SendEmailAsync(user.Email, subject, htmlContent);
        }

        public async Task ResetPasswordAsync(ResetPasswordRequestDto request)
        {
            // Find the reset token in database
            var resetToken = await _passwordResetTokensCollection
                .Find(t => t.Token == request.Token)
                .FirstOrDefaultAsync();

            if (resetToken == null || 
                resetToken.IsUsed || 
                resetToken.ExpiresAt < DateTime.UtcNow)
            {
                throw new NotFoundException("Invalid or expired password reset token.");
            }

            var user = await _usersCollection
                .Find(u => u.Id == resetToken.UserId)
                .FirstOrDefaultAsync();

            if (user == null)
                throw new NotFoundException("User not found.");

            var newPasswordHash = HashPassword(request.NewPassword);

            var userFilter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
            var userUpdate = Builders<User>.Update.Set(u => u.PasswordHash, newPasswordHash);

            await _usersCollection.UpdateOneAsync(userFilter, userUpdate);

            // Mark the token as used.
            // MongoDB TTL will automatically delete it,
            // but IsUsed protects against reuse until TTL kicks in.
            var tokenFilter = Builders<PasswordResetToken>.Filter.Eq(t => t.Id, resetToken.Id);
            var tokenUpdate = Builders<PasswordResetToken>.Update.Set(t => t.IsUsed, true);

            await _passwordResetTokensCollection.UpdateOneAsync(tokenFilter, tokenUpdate);
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSecurityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var tokenSigningCredentials = new SigningCredentials(jwtSecurityKey, SecurityAlgorithms.HmacSha256);

            // Create claims based on the user information. More claims can be added as needed (e.g., roles, permissions).
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id!),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                // Jti (JWT ID) - unique identifier for the token itself (useful for refresh token logic in the future)
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,

                // Time to live for the token (2 hours default).
                // In further development consider a shorter lifespan and implement refresh tokens.
                expires: DateTime.UtcNow.AddHours(_jwtSettings.TokenExpirationHours),

                signingCredentials: tokenSigningCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}