# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

**Run the API** (port 3000):
```
cd POS-System.Api
dotnet run
```

**Run the frontend** (port 3001):
```
cd client
npm run dev
```

**Build the solution:**
```
dotnet build POS-System.sln
```

**Run integration tests:**
```
dotnet test POS-System.IntegrationTests/POS-System.IntegrationTests.csproj
```

**Run integration test benchmark** (PowerShell):
```
.\run-integration-tests-benchmark.ps1
.\run-integration-tests-benchmark.ps1 -Iterations 10 -IncludeBuild
```

**Run EF Core migrations** (from repo root):
```
dotnet ef migrations add <MigrationName> --project POS-System.Data --startup-project POS-System.Api
dotnet ef database update --project POS-System.Data --startup-project POS-System.Api
```

## Architecture

Five-project layered solution:

```
POS-System.Api          → Controllers, middleware, DI wiring
POS-System.Business     → Services, DTOs (Request/Response), AutoMapper profiles
POS-System.Data         → EF Core DbContext, repositories, migrations, identity
POS-System.Domain       → Entity classes only
POS-System.Common       → Enums, exceptions, error detail types
```

Request flow: `Controller → IXxxService → IUnitOfWork → IXxxRepository → ApplicationDbContext (PostgreSQL)`

**Database:** PostgreSQL via Npgsql. Connection string from `ConnectionStrings:LocalConnection` in appsettings or the `DATABASE_URL` environment variable. `ApplicationDbContext` extends `IdentityDbContext<ApplicationUser, ApplicationRole, int>`.

**Authentication/Authorization:** JWT Bearer. Tokens carry individual permission claims (e.g. `TaxRead`, `TaxWrite`, `ItemRead`, `CartItemWrite`). Authorization policies are defined in `POS-System.Business/DependencyInjection.cs` and applied per-action via `[Authorize("PolicyName")]`. The JWT secret, issuer, and audience are read from configuration keys `POSJwtSecretKey`, `POSIssuer`, `POSAudience`.

**Entity versioning pattern:** Mutable entities (Tax, Product, Service, etc.) are never updated in place. Updates create a new row with the same logical ID field (e.g. `TaxId`, `ProductId`) but a new auto-increment `Id`, a new `Version` timestamp, and set `IsDeleted = false`, while the old row gets `IsDeleted = true`. This preserves full history. Queries for active records filter `IsDeleted != true`.

**Many-to-many relationships** (e.g. Product↔Tax, Service↔ItemDiscount, ProductModification↔CartItem) are handled through a generic `IManyToManyService<TLeft, TRight, TLink>` and `IGenericManyToManyRepository<TLeft, TRight, TLink>`. Link/unlink/relink operations go through this abstraction. When a versioned entity is updated, its active links are relinked to the new version via `RelinkItemToItemAsync`.

**Unit of Work:** `IUnitOfWork` exposes all typed repositories as properties and provides `SaveChangesAsync`. All service code uses `_unitOfWork.XxxRepository` and a single `SaveChangesAsync` call per operation.

**Error handling:** Services throw typed exceptions from `POS-System.Common/Exceptions/` (`NotFoundException`, `BadRequestException`, `ConflictException`, etc.). `GlobalExceptionHandler` catches these and returns structured JSON error responses with the appropriate HTTP status.

**Pagination:** `PagedResponse<T>` wraps list responses with `TotalCount`, `PageSize`, `PageNumber`, and `Data`. Controllers accept `pageNum`/`pageNumber` and `pageSize` query parameters.

## Integration Tests

The integration test project lives at `POS-System.IntegrationTests/`. Tests use `WebApplicationFactory` with an in-process test server against the real PostgreSQL database. The test appsettings at `POS-System.IntegrationTests/bin/Debug/net8.0/appsettings.json` mirrors the API's settings.

For authenticated endpoints, generate a JWT with the required claims matching the policy (`TaxRead`, `TaxWrite`, etc.) and pass it as a Bearer token. The JWT configuration (`POSJwtSecretKey`, `POSIssuer`, `POSAudience`) is shared with the API.

## Key Configuration

| Key | Value (dev) |
|---|---|
| API port | 3000 |
| DB | PostgreSQL, `Host=localhost;Port=5432;Username=postgres;Database=postgres` |
| JWT issuer | `https://auth.POS-System.com` |
| JWT audience | `https://business.com` |

Payments use Stripe (test keys in appsettings). SMS uses AWS SNS (credentials in appsettings).
