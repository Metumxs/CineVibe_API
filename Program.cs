using CineVibe.API.Configuration;
using CineVibe.API.Constants;
using CineVibe.API.Middleware;
using CineVibe.API.Services;
using CineVibe.API.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text;
using System.Text.Json.Serialization;

namespace CineVibe.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Bind MongoDB settings from User Secrets/appsettings to the MongoDbSettings class
            builder.Services.Configure<MongoDbSettings>(
                builder.Configuration.GetSection("MongoDB"));

            // Bind JWT settings to the JwtSettings class
            builder.Services.Configure<JwtSettings>(
                builder.Configuration.GetSection("JwtSettings"));

            builder.Services.Configure<TmdbSettings>(
                builder.Configuration.GetSection("TmdbSettings"));
            builder.Services.Configure<CacheSettings>(
                builder.Configuration.GetSection("CacheSettings"));
            builder.Services.Configure<SendGridSettings>(
                builder.Configuration.GetSection("SendGrid"));
            builder.Services.Configure<AppSettings>(
                builder.Configuration.GetSection("AppSettings"));

            // Load mood tags from separate config file.
            // Kept separate from appsettings.json for independent versioning
            // and future migration to MongoDB if admin panel is added.
            builder.Configuration.AddJsonFile("moodtagssettings.json", optional: false, reloadOnChange: true);

            builder.Services.Configure<MoodTagsSettings>(
                builder.Configuration.GetSection("MoodTagsSettings"));

            // Resolve genre names to IDs once at startup and register as singleton.
            // Services inject List<ResolvedMoodTag> directly — no runtime resolution needed.
            builder.Services.AddSingleton<List<ResolvedMoodTag>>(sp =>
            {
                var settings = sp.GetRequiredService<IOptions<MoodTagsSettings>>().Value;

                return settings.MoodTags.Select(mtc => new ResolvedMoodTag
                {
                    Id = mtc.Id,
                    Label = mtc.Label,
                    Emoji = mtc.Emoji,
                    Description = mtc.Description,
                    GroupId = mtc.GroupId,
                    // Era, scale, and length tags have no GenreNames — GenreIds will be an empty list.
                    // Genres are always sourced exclusively from the tone tag at query time.
                    GenreIds = TmdbConstants.ResolveGenreIds(mtc.GenreNames.ToArray()),
                    // Retained as artifact — not used in current algorithm.
                    KeywordIds = mtc.KeywordIds,
                    SortBy = mtc.SortBy,
                    VoteCountMin = mtc.VoteCountMin,
                    VoteCountMax = mtc.VoteCountMax,
                    VoteAverageMin = mtc.VoteAverageMin,
                    RuntimeMinMinutes = mtc.RuntimeMinMinutes,
                    RuntimeMaxMinutes = mtc.RuntimeMaxMinutes,
                    ReleaseDateMin = mtc.ReleaseDateMin,
                    ReleaseDateMax = mtc.ReleaseDateMax
                }).ToList();
            });

            // Configure CORS policy to allow requests from the frontend application
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy => policy
                        .WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod());
            });

            // Register MongoDB Client as a Singleton
            builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
            {
                // Fetch our settings
                var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
                return new MongoClient(settings.ConnectionString);
            });

            // Register specific MongoDB Database as Scoped (or Singleton)
            // That allows us to directly inject IMongoDatabase into our services without having to resolve the client and database separately.
            builder.Services.AddScoped<IMongoDatabase>(serviceProvider =>
            {
                var settings = serviceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
                var client = serviceProvider.GetRequiredService<IMongoClient>();
                return client.GetDatabase(settings.DatabaseName);
            });

            // Register standard services for Controllers and API documentation
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.AddSwaggerGen(o =>
            {
                // Add a security definition for JWT Bearer tokens to enable the "Authorize" button in Swagger UI
                o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme." +
                    "Enter 'Bearer' [SPACE] and your token in the text field below.\r\n\r\n" +
                    "EXAMPLE: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                // Demand that the "Bearer" token is included in the header of all requests to protected endpoints.
                // This will prompt users to provide a token when trying to access secured API methods via Swagger UI.
                o.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });

            // Configure JWT Authentication
            // Mapping configuration directly to our strongly-typed class:
            var jwtOptions = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
            var jwtSecretKey = jwtOptions?.Key ?? throw new InvalidOperationException("JWT Key is missing in configuration.");

            // Configure the authentication scheme
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
                };
            });

            // Register custom services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddAuthorization();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IEmailService, SendGridEmailService>();
            builder.Services.AddScoped<IMovieService, MovieService>();
            builder.Services.AddScoped<IRouletteService, RouletteService>();
            builder.Services.AddScoped<IMoodMatcherService, MoodMatcherService>();


            // Fetching TMDB configuration values
            var tmdbBaseUrl = builder.Configuration["TmdbSettings:BaseUrl"];
            var tmdbToken = builder.Configuration["TmdbSettings:BearerToken"];

            // Registering a named HttpClient for TMDB integration
            // Using IHttpClientFactory prevents socket exhaustion and centralizes HTTP configuration
            builder.Services.AddHttpClient("TmdbClient", client =>
            {
                // Validate BaseUrl to prevent runtime Uri format exceptions
                if (!string.IsNullOrEmpty(tmdbBaseUrl))
                {
                    client.BaseAddress = new Uri(tmdbBaseUrl);
                }

                // Force API to return JSON format
                client.DefaultRequestHeaders.Add("Accept", "application/json");

                // Centralized Authorization header setup
                // All requests via "TmdbClient" will automatically attach this Bearer token
                if (!string.IsNullOrEmpty(tmdbToken))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tmdbToken);
                }
            });

            builder.Services.AddHttpClient("SendGridClient");

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            // Disable https redirection in development to avoid issues with self-signed certificates and simplify local testing.
            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }


            /**************************************************************/
            // IMPORTANT :
            // Middleware pipeline order MATTERS - do not reorder without understanding the implications.

            // Global exception handling middleware should be registered early in the pipeline
            // to catch exceptions from all subsequent middleware and controllers.
            app.UseExceptionHandler();

            // CORS middleware must be placed BEFORE Authorization
            app.UseCors("AllowFrontend");

            // These two lines must be in this specific order!
            app.UseAuthentication(); // First, we check WHO you are (token validation)
            app.UseAuthorization();  // Then we check if you have the RIGHT to access the resource (roles, policies, etc.)
            
            app.MapControllers();
            /**************************************************************/


            // Sanity check: Ping the MongoDB database on startup
            try
            {
                using var scope = app.Services.CreateScope();
                var mongoClient = scope.ServiceProvider.GetRequiredService<IMongoClient>();
                // Ping database. If the password or IP is incorrect, it will fail here.
                // Note: RunCommandAsync returns a Task, so we wait on it to ensure the ping completes before proceeding.
                mongoClient.GetDatabase("admin").RunCommandAsync((Command<BsonDocument>)"{ping:1}").Wait();
                Console.WriteLine("Successfully connected to MongoDB Atlas!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to MongoDB: {ex.Message}");
            }


            app.Run();
        }
    }
}