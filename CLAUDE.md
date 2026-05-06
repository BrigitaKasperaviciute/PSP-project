# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend (ASP.NET Core)

```bash
# Run the API (from solution root or POS-System.Api/)
dotnet run --project POS-System.Api

# Build entire solution
dotnet build

# Run all integration tests
dotnet test POS-System.IntegrationTests

# Run a single test by name
dotnet test POS-System.IntegrationTests --filter "FullyQualifiedName~GetAllTaxes_WhenTaxesExist_ReturnsOkWithPagedResults"

# Run tests for a specific controller
dotnet test POS-System.IntegrationTests --filter "FullyQualifiedName~TaxControllerTests"

# Add a new EF Core migration
dotnet ef migrations add <MigrationName> --project POS-System.Data --startup-project POS-System.Api

# Apply migrations
dotnet ef database update --project POS-System.Data --startup-project POS-System.Api
```

### Frontend (Next.js)

```bash
cd client
npm run dev     # dev server on http://localhost:3001
npm run build
npm run lint
```

## Architecture

The solution has five .NET projects and one Next.js frontend:

| Project | Role |
|---|---|
| `POS-System.Api` | ASP.NET Core Web API, port 3000. Controllers, middleware, Swagger. |
| `POS-System.Business` | Services, DTOs, AutoMapper profiles, FluentValidation validators, JWT auth setup. |
| `POS-System.Data` | EF Core `ApplicationDbContext`, repositories, Unit of Work, Identity, migrations, seeder. |
| `POS-System.Domain` | POCO entities only — no logic. |
| `POS-System.Common` | Enums, custom exceptions (`NotFoundException`, `BadRequestException`, etc.), `ErrorDetails`. |
| `client/` | Next.js 15 / React 19 frontend, talks to the API. |
| `POS-System.IntegrationTests` | xUnit integration tests using Testcontainers (PostgreSQL) + WebApplicationFactory. |

### Request flow

`Controller` → `IXxxService` (Business layer) → `IXxxRepository` / `IUnitOfWork` (Data layer) → PostgreSQL

### Key patterns

**Versioning** — `Product`, `Tax`, `Service`, `ProductModification`, and `ItemDiscount` are append-only versioned entities. Each logical record has a stable `ProductId`/`TaxId`/etc. and multiple rows with different `Id` values (versions). `IsDeleted` soft-deletes a version. The latest non-deleted row is the active version.

**Unit of Work** — `IUnitOfWork` wraps all repositories. Services inject it instead of individual repositories for write operations.

**Authorization policies** — Defined in `POS_System.Business.DependencyInjection`. Each policy requires a single claim (e.g., `"TaxRead"`, `"TaxWrite"`). Claims are issued in the JWT at login.

**Exception handling** — Services throw typed exceptions from `POS_System.Common.Exceptions` (`NotFoundException`, `BadRequestException`, `ConflictException`, etc.). `GlobalExceptionHandler` maps them to JSON `ErrorDetails` responses.

**Seeded data** — `POS-System.Data/Database/Seeder.cs` seeds baseline data via `HasData`. Tests run against a real PostgreSQL container (Testcontainers); seeded records are present after `MigrateAsync()`. Seeded IDs: Taxes 1–4, Products 1–4, Services 1–4, Carts 1–4, CartItems 1–4, Employees 1–5, ProductModifications 1–5, TimeSlots 1–4, ItemDiscounts 1–4.

**Third-party integrations** — Stripe (payments), AWS SNS (`SmsService`, concrete class), NETCore.MailKit (`IEmailSender`). In integration tests, `IEmailSender` is replaced with a Moq mock; `SmsService` suppresses its own exceptions so it fails silently with fake credentials.

### Configuration keys

`POSJwtSecretKey`, `POSIssuer`, `POSAudience` — JWT.  
`ConnectionStrings:LocalConnection` — overridden by `DATABASE_URL` env var at runtime.  
`Stripe:SecretKey`, `AWS:*`, `EmailConfiguration:*` — third-party.  
`FileProvider:Events:Path`, `FileProvider:Exceptions:Path` — file-based logging.

### Integration test setup

Tests live in `POS-System.IntegrationTests/`. The `ApiFactory` (in `Infrastructure/`) spins up a Testcontainers PostgreSQL instance, runs migrations, replaces the DB context, swaps authentication to a `TestAuthenticationHandler` (grants all policy claims when `Authorization: Test <role>` header is present, returns 401 otherwise), and mocks `IEmailSender`.

Fake config values for JWT / Stripe / AWS / Email are injected via `ConfigureAppConfiguration` so `TryGetConfigValue` in Business DI does not call `Environment.Exit`.
