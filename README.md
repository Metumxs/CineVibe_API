# CineVibe — Backend API

Backend of a movie discovery web app built as a **team project during the NIX Solutions internship**.

CineVibe helps users find films by mood, run a "movie roulette" for decision fatigue, and manage personal watchlists with a "time left" tracker. This repository contains the ASP.NET Core Web API that powers the frontend.

> **Context:** this is a team project. My contribution is listed below; the frontend and some features were built collaboratively.

## My contribution

I implemented the full backend of the project:

- **Auth & users** — JWT authentication, BCrypt password hashing, password reset flow with expiring one-time tokens (SendGrid), profile management.
- **Movies** — TMDB integration (catalog, details, search), MongoDB cache-aside layer with `SemaphoreSlim` thundering-herd protection, background persistence, and DTO mapping.
- **Mood matcher** — designed the mood-tag model (tone / era / scale / length groups), the parameter-merging logic, and the 3-level TMDB fallback cascade with validators for contradictory tag combinations.
- **Watchlist** — watched/watchlist CRUD, bulk keep/discard update, "Time Left Tracker" runtime stats, and the watchlist optimizer (prioritized removal candidates based on `DetailsOpenedAt` and cache runtime).
- **Infrastructure** — global exception handler (`IExceptionHandler` + `ProblemDetails`), Swagger with JWT auth, CORS, MongoDB startup health check, and configuration via User Secrets.

**Roulette:** originally mine, but the first API contract with the frontend didn't fully align (question/answer ordering, session-level exclusions). The feature was partially reworked in collaboration with the frontend team to settle the contract.

## Tech stack

- .NET 8 / ASP.NET Core Web API
- MongoDB (Atlas) via `MongoDB.Driver` 3.x
- TMDB API for movie data
- SendGrid for transactional email
- JWT Bearer authentication
- Swagger / OpenAPI
- BCrypt.Net for password hashing

## Features

- **Auth** — register, login, forgot/reset password.
- **Movies** — paginated trending catalog with genre / rating / year filters and sorting, details with trailer, search. Results are cached opportunistically in MongoDB.
- **Mood matcher** — combine up to four mood tags (one per group) → merged TMDB `discover` query with 3-level fallback so users always see something.
- **Roulette** — random 3-question quiz → TMDB `discover` with fallback cascade → single recommendation, excluding already-watched and session-excluded movies.
- **Users** — profile, watched list, watchlist, bulk update, watchlist stats, watchlist optimizer.
- **Global error handling** — RFC 7807 `ProblemDetails` for `ConflictException`, `NotFoundException`, `UnauthorizedAccessException`, `ArgumentException`, and unhandled errors.

## Project structure

```
Configuration/     Strongly-typed settings classes
Constants/         TMDB constants, roulette question banks
Controllers/       HTTP endpoints
DTOs/              Request/response contracts
Exceptions/        Domain exceptions (Conflict, NotFound)
Middleware/        Global exception handler
Models/            MongoDB documents
Services/          Business logic
  Interfaces/      Service contracts
Templates/         Email templates
Program.cs         Composition root
```

## Endpoints (summary)

| Area     | Endpoint                                                                                                                               | Auth     |
|----------|----------------------------------------------------------------------------------------------------------------------------------------|----------|
| Auth     | `POST /api/auth/register`, `/login`, `/forgot-password`, `/reset-password`                                                             | —        |
| Movies   | `GET /api/movies/trending`, `/api/movies/{id}`, `/api/movies/search`                                                                  | —        |
| Mood     | `GET /api/mood/moodtags`, `/api/mood/movies`                                                                                          | —        |
| Roulette | `GET /api/roulette/questions`, `POST /api/roulette/recommend`                                                                         | JWT      |
| Users    | `GET/PUT /api/users/profile`, watched & watchlist endpoints, `GET /api/users/watchlist/stats`, `/watchlist/optimizer`, `POST /watchlist/bulk-update` | JWT      |
| Dev      | `GET /api/dev/keywords/resolve`, `GET /api/dev/cache/warm-up/movies-runtime`                                                          | dev only |

## Configuration

**No secrets are committed.** All sensitive values are loaded from **User Secrets** in development and from environment variables in production. The committed `appsettings.json` contains only section names and non-sensitive defaults.

Required keys:

| Key                        | Where                  |
|----------------------------|------------------------|
| `MongoDB:ConnectionString` | User Secrets / env     |
| `MongoDB:DatabaseName`     | User Secrets / env     |
| `JwtSettings:Key`          | User Secrets / env     |
| `TmdbSettings:BearerToken` | User Secrets / env     |
| `SendGrid:ApiKey`          | User Secrets / env     |

Mood tag definitions live in `moodtagssettings.json` (versioned separately so the tag catalog can be edited without touching secrets).

## Running locally

```bash
dotnet restore

dotnet user-secrets set "MongoDB:ConnectionString" "mongodb+srv://..."
dotnet user-secrets set "MongoDB:DatabaseName"     "CineVibeDB"
dotnet user-secrets set "JwtSettings:Key"          "<64-byte random string>"
dotnet user-secrets set "TmdbSettings:BearerToken" "<tmdb bearer token>"
dotnet user-secrets set "SendGrid:ApiKey"          "<sendgrid api key>"

dotnet run
```

Swagger UI: `https://localhost:<port>/swagger` in Development.

## Design notes

- **Cache-aside with `SemaphoreSlim`.** Catalog entries are refreshed every 24h (`CacheSettings`). Only one request refreshes at a time; concurrent requests wait and reuse the result.
- **`RuntimeMinutes` is populated opportunistically.** TMDB list endpoints don't return runtime, so it's only filled in when a movie's details page is opened. The "Time Left Tracker" and watchlist optimizer build on that.
- **Fallback cascades** in Mood matcher and Roulette always return *something* — the response includes a `FallbackLevel` so the frontend can show a "we widened the search" hint.

## Limitations

- No automated tests yet — the repo focuses on end-to-end API behaviour and manual verification.
- The `Dev` controller is intended for local use only (guarded by `IWebHostEnvironment.IsDevelopment()`).
- The frontend is in a separate repository.

## License

Portfolio / educational use. Not affiliated with TMDB or NIX Solutions.
