# SolidShortener

A production-deployed URL shortener API built with **.NET 8** and **Clean Architecture**. Demonstrates real-world backend patterns: CQRS, repository decorator pattern for caching, JWT authentication, rate limiting, and structured error handling.

**Live API:** https://shrinkit-1cmp.onrender.com

> ⚠️ Hosted on Render's free tier — expect a 1–2 minute cold start after 15 minutes of inactivity.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Runtime | .NET 8 |
| Database | PostgreSQL via [Supabase](https://supabase.com) |
| Cache | Redis via [Upstash](https://upstash.com) |
| Hosting | [Render](https://render.com) (Dockerized) |
| ORM | Entity Framework Core 9 + Npgsql |
| Auth | JWT Bearer tokens |
| Password hashing | BCrypt |
| DI decoration | Scrutor |
| API docs | Swagger / OpenAPI |

---

## Architecture

Clean Architecture with four layers, each depending only inward:

```
SolidShortener.Domain          — Entities, domain logic, no external dependencies
SolidShortener.Application     — Use cases, interfaces, DTOs, CQRS commands/queries
SolidShortener.Infrastructure  — EF Core, Redis, JWT, BCrypt implementations
SolidShortener.Api             — Controllers, middleware, filters, DI composition root
```

Dependency rule enforced at the project reference level — Infrastructure never leaks into Application, Domain has zero framework dependencies.

---

## Key Design Decisions

### Repository Decorator Pattern (Caching)
Rather than injecting `IDistributedCache` into services, caching is implemented as a transparent decorator over `IUrlRepository`:

```csharp
services.AddScoped<IUrlRepository, UrlRepository>();
services.Decorate<IUrlRepository, CachedUrlRepository>(); // Scrutor
```

`CachedUrlRepository` wraps every read with a Redis lookup and invalidates on write. Services have no knowledge of caching — swapping the strategy requires zero application-layer changes.

### CQRS-lite
Operations are separated into explicit Commands (writes) and Queries (reads) without a full mediator dependency:

```
ShortenUrlCommand / DeleteUrlCommand
GetUrlByShortCodeQuery / GetUrlsByUserQuery / GetVisitsByShortCodeQuery
```

This keeps intent explicit and makes the codebase navigable without MediatR overhead for a project of this scope.

### Scoped Lifetime for Background Work
Visit logging is fire-and-forget — the redirect should not wait on analytics. The naive approach of discarding a Task on a scoped service causes `ObjectDisposedException` when the HTTP request scope is torn down mid-execution. The fix creates a dedicated scope:

```csharp
_ = Task.Run(async () =>
{
    using var scope = _scopeFactory.CreateScope();
    var visitService = scope.ServiceProvider.GetRequiredService<IVisitService>();
    await visitService.LogVisitAsync(command);
});
```

### Domain Behavior on Entities
Entities are not anemic data bags. Business operations are methods:

```csharp
url.MarkAsDeleted();
url.UnDelete();
url.IncrementVisitsCount();
url.SetExpiresAt(expiresAt);
```

State transitions are encapsulated and auditable via `UpdatedAt` tracking.

### Idempotent URL Shortening
If a user shortens a URL they've previously deleted, the record is restored rather than duplicated:

```csharp
if (existing.IsDeleted) existing.UnDelete();
existing.SetExpiresAt(command.ExpiresAt);
await _urlRepository.UpdateAsync(existing);
```

### Token Expiry as Single Source of Truth
`ITokenGenerator` returns a `(string Token, DateTime ExpiresAt)` tuple, so `AuthResultDTO.ExpiresAt` is always derived from the same config value used to sign the JWT — never a hardcoded constant that can silently diverge.

---

## API Reference

Base URL: `https://shrinkit-1cmp.onrender.com`

Interactive docs available at the root URL via Swagger UI.

### Authentication

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/users/register` | ❌ | Register a new account |
| `POST` | `/api/users/login` | ❌ | Login and receive a JWT |

### URLs

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/url/shorten` | ✅ | Shorten a URL |
| `GET` | `/api/url/{code}` | ❌ | Redirect to original URL |
| `GET` | `/api/url` | ✅ | List all URLs for the authenticated user |
| `DELETE` | `/api/url/{code}` | ✅ | Soft-delete a shortened URL |

### Analytics

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/visit/count/{shortCode}` | ✅ | Get total visit count |
| `GET` | `/api/visit/visits/{shortCode}` | ✅ | Get full visit history |

**Authenticated endpoints** require `Authorization: Bearer <token>` header.

---

## Running Locally

**Prerequisites:** .NET 8 SDK, PostgreSQL, Redis (or Docker for both)

```bash
git clone https://github.com/syedhhassan/url-shortener
cd url-shortener
```

Set secrets via dotnet user-secrets (never commit connection strings):

```bash
cd src/SolidShortener.Api

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Database=solidshortener;Username=postgres;Password=yourpassword"
dotnet user-secrets set "ConnectionStrings:CacheConnection" "localhost:6379,abortConnect=False"
dotnet user-secrets set "JwtSettings:SecretKey" "your-secret-key-min-32-chars"
```

Run migrations:

```bash
cd ../..
dotnet ef database update \
  --project src/SolidShortener.Infrastructure \
  --startup-project src/SolidShortener.Api
```

Start the API:

```bash
dotnet run --project src/SolidShortener.Api
```

Swagger UI: `http://localhost:5109`

---

## Tests

```bash
dotnet test
```

Unit tests cover `UrlService` and `UserService` using xUnit + Moq with mocked repositories — no database required. Scenarios covered:

- New URL creation (verify Add + Update called)
- Idempotent re-shortening of a soft-deleted URL
- Expired URL resolution returning null
- Unauthorized delete attempt (wrong user)
- Non-existent short code throwing `KeyNotFoundException`
- Successful authentication returning token
- Wrong password returning null
- Unknown email returning null
- Duplicate email registration throwing `ConflictException`

---

## Deployment

Containerized via a multi-stage Dockerfile. The build stage compiles with the full SDK; the runtime stage ships only the ASP.NET runtime (~200MB smaller image).

Environment is configured entirely through environment variables — no secrets in source:

```
ConnectionStrings__DefaultConnection   → Supabase Postgres (Session pooler)
ConnectionStrings__CacheConnection     → Upstash Redis (StackExchange format)
JwtSettings__SecretKey                 → Signing key (min 32 chars)
JwtSettings__Issuer                    → Token issuer
JwtSettings__Audience                  → Token audience
JwtSettings__ExpiryMinutes             → Token lifetime
ASPNETCORE_ENVIRONMENT                 → Production
```

---

## Project Structure

```
src/
├── SolidShortener.Domain/
│   └── Entities/               User, Url, Visit with domain methods
│   └── Exceptions/             Custom domain exceptions
│
├── SolidShortener.Application/
│   ├── Interfaces/             IUrlRepository, IUserRepository, ITokenGenerator...
│   ├── Urls/                   Commands, Queries, DTOs, UrlService
│   ├── Users/                  Commands, Queries, DTOs, UserService
│   └── Visits/                 Commands, Queries, DTOs, VisitService
│
├── SolidShortener.Infrastructure/
│   ├── Persistence/            EF Core DbContext + Fluent API configurations
│   ├── Repositories/           UrlRepository, UserRepository, VisitRepository
│   │   └── Decorators/         CachedUrlRepository (Redis decorator)
│   ├── Services/               JwtTokenGenerator, BCryptPasswordHasher, Base62ShortCodeGenerator
│   └── Migrations/
│
└── SolidShortener.Api/
    ├── Controllers/            UrlController, UserController, VisitController
    ├── Middlewares/            ErrorHandlingMiddleware, RateLimitingMiddleware
    ├── Filters/                ValidationFilter
    ├── Extensions/             DI registration extensions
    ├── Models/ 
    └── Services/


tests/
└── SolidShortener.Tests/       Unit tests (xUnit + Moq)
```

---

## Author

**Syed Hassan** — [GitHub](https://github.com/syedhhassan)