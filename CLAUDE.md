# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Backend
```bash
# Build entire solution
dotnet build

# Run the API (from repo root)
dotnet run --project POS-System.Api

# Run integration tests
dotnet test POS-System.IntegrationTests/

# Run a single test
dotnet test POS-System.IntegrationTests/ --filter "FullyQualifiedName~GetAll_WithValidAuth_ReturnsOk"

# EF Core migrations (run from repo root)
dotnet ef migrations add <MigrationName> --project POS-System.Data --startup-project POS-System.Api
dotnet ef database update --project POS-System.Data --startup-project POS-System.Api
```

### Frontend
```bash
cd client
npm install
npm run dev      # dev server on port 3001
npm run build
npm run lint
```

## Architecture

The solution has five layers with strict unidirectional dependencies:

```
Api → Business → Data → Domain
                      → Common
```

- **POS-System.Api** — Controllers, middleware, exception handler, DI configuration. Runs on `http://localhost:3000`.
- **POS-System.Business** — Services, DTOs (Request/Response), FluentValidation validators, AutoMapper profiles, JWT token generation.
- **POS-System.Data** — `ApplicationDbContext` (EF Core + ASP.NET Identity), repositories, migrations, seeders.
- **POS-System.Domain** — Entity records with EF Core annotations.
- **POS-System.Common** — Enums, custom exceptions (`BaseException` subclasses), `ErrorDetails` error response type.

## Entity Versioning Pattern

Most domain entities (`Product`, `Tax`, `Service`, `ItemDiscount`, `ProductModification`) use an append-only versioning pattern:

- `Id` — surrogate primary key (each version is a distinct row)
- `ProductId` / `TaxId` / etc. — stable logical identifier shared across versions (auto-incremented via `UseIdentityColumn`)
- `Version` — timestamp of this version
- `IsDeleted` — soft-delete flag; the latest version with `IsDeleted = false` is the "active" record

`Cart` and `CartItem` implement `ILinkable` but are not actually versioned (they keep `IsDeleted` to satisfy the interface for the generic many-to-many logic).

## Data Layer Patterns

**Unit of Work** (`POS-System.Data/Repositories/UnitOfWork.cs`): Single `SaveChangesAsync()` call for transactional scope. Constructor-injected with all ~20 repositories. Always call `SaveChangesAsync()` after mutations.

**Generic Repository** (`Repository<T>`): Methods include `GetByIdAsync`, `GetByExpressionAsync`, `GetAllWithIncludesAndPaginationAsync` (returns `(List<T> Results, int TotalCount)`), and `Delete`. Eager loading via `params Expression<Func<T, object>>[] includes`.

**BusinessDetailRepository** is file-based (JSON), not EF — controlled by config keys `BusinessDetails:FileName` and `BusinessDetails:RelativePath`.

## Authentication & Authorization

JWT Bearer authentication. Authorization is **claim-based**, not role-based. Each policy requires a specific claim type to be present:

```csharp
options.AddPolicy("TaxRead", policy => policy.RequireClaim("TaxRead"));
options.AddPolicy("TaxWrite", policy => policy.RequireClaim("TaxWrite"));
// etc.
```

Full list of claim policies: `TransactionRead/Write`, `HistoricTransactionRead/Write`, `ServiceRead/Write`, `ItemRead/Write`, `EmployeesRead/Write`, `TaxRead/Write`, `GiftCardRead/Write`, `CartItemRead/Write`, `ItemDiscountRead/Write`, `BusinessDetailsRead/Write`.

JWT token config keys: `POSJwtSecretKey`, `POSIssuer`, `POSAudience`.

**`CartController` and `CartDiscountController` have no `[Authorize]` attributes** — they are publicly accessible.

## Error Response Format

All errors use `ErrorDetails` (not ASP.NET ProblemDetails):

```csharp
public class ErrorDetails {
    public string Title { get; set; }
    public int Status { get; set; }
    public string? Details { get; set; }
    public string? ParameterName { get; set; }
}
```

Exception subclasses in `POS-System.Common/Exceptions/`: `BadRequestException` (400), `NotFoundException` (404), `ConflictException` (409), `UnauthorizedException` (401), `TooManyRequestsException` (429), `InternalServerErrorException` (500). Only `BaseException` subclasses produce structured `ErrorDetails`; plain `Exception` returns a generic 500.

## Seed Data

The `Seeder` class seeds data via EF Core model builder (applied on `MigrateAsync`):

- **Employees** (ApplicationUser): IDs 1–5, usernames `johndoe`, `janedoe`, `adamsmith`, `bobjohnson`, `johnsondoe`. Stored password hashes are non-functional; use the register endpoint to create testable users.
- **Taxes**: IDs 1–4 (Tax1, Tax2, Tax3, Tax1 v2)
- **Products**: IDs 1–4 (Product1, Product2, Product1 v2, Product1 v3)
- **Services**: IDs 1–4
- **Carts**: IDs 1–4 (reference employee IDs 1–4)
- **CartItems**: IDs 1–4
- **TimeSlots**: IDs 1–4
- **ItemDiscounts**: IDs 1–4

## Third-Party Integrations

- **Stripe** (`Stripe:SecretKey` config) — used in `PaymentService` for card checkout, refunds, and session management. `RegisterCashTransactionAsync` does **not** call Stripe.
- **AWS SNS** (`AWS:Region/AccessKeyId/SecretAccessKey/SessionToken`) — used in `SmsService` (singleton, connects at call time)
- **Email** (`EmailConfiguration` section) — `IEmailSender` / `EmailSender` in `POS_System.Business.Services.Interfaces`

## Frontend

**Stack**: Next.js 15 (App Router), React 19, Tailwind CSS, SASS.

**Auth flow**: `client/src/middleware.ts` validates JWT on every request and redirects unauthenticated users to `/login`. Set `REQUIRE_AUTH=NO` to bypass in local dev.

**API layer**: Custom API classes (e.g. `AuthApi`, `ProductApi`) wrap `fetch`. Custom hooks per feature (e.g. `useProducts`) call these classes. No global state manager — React hooks only.

**Third-party**: `@stripe/stripe-js` for payment UI, `jose` for JWT verification, `luxon` for dates, `cookies-next` for cookie access.

## Integration Tests

Tests live in `POS-System.IntegrationTests/`. The factory (`ApiFactory`) boots the real API against a disposable PostgreSQL Testcontainer. Only `IEmailSender` is mocked. Authentication is replaced with a `TestAuthHandler` scheme that reads claim names from the `Authorization` header.

Use `factory.CreateClientWithClaims("TaxRead", "TaxWrite")` for authorized clients, `factory.CreateAdminClient()` for a client with all system claims, and `factory.CreateClient()` for unauthenticated calls (returns 401 on protected endpoints).

Test data builders live in `POS-System.IntegrationTests/Builders/` (e.g., `ProductBuilder`, `TaxBuilder`, `ServiceBuilder`). Every test class cleans its own tables in `InitializeAsync` using `ExecuteDeleteAsync` in FK-safe dependency order.

### Required cleanup order (FK constraints)

When a test class needs to reset state:
```csharp
// Service-related
await db.ServiceReservations.ExecuteDeleteAsync();
await db.CartItems.Where(ci => ci.ServiceVersionId != null).ExecuteDeleteAsync();
await db.ServiceOnTaxes.ExecuteDeleteAsync();
await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
await db.EmployeeOnServices.ExecuteDeleteAsync();
await db.Services.ExecuteDeleteAsync();

// Product-related
await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
await db.ProductModifications.ExecuteDeleteAsync();
await db.CartItems.Where(ci => ci.ProductVersionId != null).ExecuteDeleteAsync();
await db.ProductOnTaxes.ExecuteDeleteAsync();
await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
await db.Products.ExecuteDeleteAsync();

// Cart
await db.CartItems.ExecuteDeleteAsync();
await db.Carts.ExecuteDeleteAsync();
```

Note: `db.CardDiscounts` (typo in production code) is the DbSet for `CartDiscount` entities.
