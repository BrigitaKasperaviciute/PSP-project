# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build the full solution
dotnet build POS-System.sln

# Run the API
dotnet run --project POS-System.Api

# Run all integration tests
dotnet test POS-System.Integration.Tests/POS-System.Integration.Tests.csproj

# Run a single test class
dotnet test POS-System.Integration.Tests --filter "FullyQualifiedName~TaxControllerTests"

# Run a single test method
dotnet test POS-System.Integration.Tests --filter "FullyQualifiedName~GetAll_ValidToken_ReturnsOkWithPagedResult"
```

The API runs on `http://localhost:3000` by default. Swagger UI is available at `/swagger` in Development.

## Solution Structure

Five projects with a clean layered architecture:

- **POS-System.Api** — ASP.NET Core 8 Web API. Controllers, exception handler, DI configuration, Swagger, validators.
- **POS-System.Business** — Application logic. Services, AutoMapper profiles, DTOs (Request/Response), and utility classes.
- **POS-System.Data** — EF Core 8 + Npgsql. `ApplicationDbContext`, repositories, unit of work, seeder, and relationship linker.
- **POS-System.Domain** — Entity classes only. No logic.
- **POS-System.Common** — Shared enums, constants, custom exceptions (`BadRequestException`, `NotFoundException`, etc.), and `ErrorDetails`.
- **POS-System.Integration.Tests** — xUnit integration tests using `WebApplicationFactory<Program>`.

## Key Architectural Patterns

**Exception → HTTP mapping** — All domain exceptions extend `BaseException` in `POS-System.Common`. The `GlobalExceptionHandler` maps them to HTTP status codes (`NotFoundException` → 404, `BadRequestException` → 400, etc.). Unknown exceptions become 500.

**Authorization** — JWT Bearer with claim-based policies. Each policy name is a single string (e.g. `"TaxRead"`, `"ServiceWrite"`). Claims are seeded on Identity roles via `Seeder` / `SeedRolesWithClaimsAsync`. The claim value must equal `"Y"` to pass authorization.

**Entity versioning** — Many entities use a `(Id, EntityId, Version, IsDeleted)` pattern instead of hard deletes. `Id` is the primary key (auto-incremented); `EntityId` groups versions of the same logical entity; `IsDeleted` soft-deletes a version. Active records have `IsDeleted = false`.

**Repository / Unit of Work** — `IUnitOfWork` (in `POS-System.Data`) aggregates all typed repositories. Services receive `IUnitOfWork` and call `SaveChangesAsync()` after mutations.

**CartDiscount DbSet typo** — `ApplicationDbContext` has `public DbSet<CartDiscount> CardDiscounts { get; set; }` (note `Card`, not `Cart`). Use `db.CardDiscounts` in tests and data access code.

## Integration Test Setup

All test infrastructure lives in `POS-System.Integration.Tests/Infrastructure/`.

**`IntegrationTestFactory`** — `WebApplicationFactory<Program>` subclass that:
- Replaces PostgreSQL with EF Core InMemory (`UseInMemoryDatabase` with a unique GUID db name per factory instance)
- Replaces `IEmailSender` with `NoOpEmailSender`
- Provides fake config (JWT keys, Stripe, AWS, email)
- `EnsureInitializedAsync()` creates the schema and seeds all 5 Identity roles (`Super admin`, `Service provider`, `Cashier`, `Owner`, `None`) with all 20 permission claims each set to `"Y"`

**`TestAuthHelper`** — Generates JWT tokens signed with the app's test secret key. `AddFullAccessAuth(HttpClient)` attaches a Bearer token with all permission claims to an `HttpClient`.

**Pattern per test class:**
```csharp
public class FooControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly HttpClient _authClient; // authenticated
    private readonly HttpClient _anonClient; // no token

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
```

Tests within a class share the same in-memory database (via `IClassFixture`). Each test that mutates data should create its own records rather than relying on mutations from other tests. Read-only tests can use seeded data.

**`POS-System.Api/ProgramAccessor.cs`** — Contains `public partial class Program {}` to expose the top-level `Program` class for `WebApplicationFactory<Program>`.

## Seeded Test Data

Key seeded records for integration tests (from `Seeder.cs`):

| Entity | Active records |
|--------|---------------|
| Tax | Id=2 (`TaxId=2`, "Tax2"), Id=4 (`TaxId=1`, "Tax1 v2") |
| Product | Id=4 (`ProductId=1`, "Product1 v3", `IsDeleted=false`) |
| Service | Id=1 ("Service1", `EmployeeId=1`), Id=4 ("Service2 v2") |
| ProductModification | Id=2 ("Extra cheese v2"), Id=4 ("No cheese v2"), Id=5 ("Extra fork") |
| TimeSlot | Id=1,2 (`IsAvailable=true`), Id=3 (`IsAvailable=false`), Id=4 (`IsAvailable=true`) |
| ItemDiscount | Id=2 (`IsDeleted=false`), Id=3 (`IsDeleted=false`) |
| Cart | Id=1 (PENDING), Id=2 (COMPLETED), Id=3 (IN_PROGRESS), Id=4 (PENDING) |
| CartItem | Id=1 (Cart=1, Product), Id=2 (Cart=1, Service), Id=3,4 (Cart=2) |
| Employee (ApplicationUser) | Id=1 "johndoe", Id=2 "janedoe", Id=3 "adamsmith", Id=4 "bobjohnson" |

Seeded employee password hashes are legacy format — create fresh users via `CreateTestUserAsync` or `POST /api/employees/register` for login tests.

## External Services

- **Stripe** — Used directly (no interface) in `PaymentService`. Tests that hit Stripe endpoints (full-checkout, partial-checkout, refund with `IsCard=true`) will fail unless a real key is configured. Test only the pre-Stripe validation paths (e.g. non-existent cart → 404, wrong cart status → 400).
- **AWS SNS (`SmsService`)** — Catches all exceptions internally; safe to call with fake credentials.
- **`IEmailSender`** — Replaced with `NoOpEmailSender` in tests.
