# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Point of Sale (POS) system built by "25rd United LT". ASP.NET Core 8 backend + Next.js 15 frontend.

## Commands

### Backend (ASP.NET Core)
```bash
cd POS-System.Api && dotnet run          # Run API on http://localhost:3000
dotnet build                              # Build solution
dotnet test                              # Run tests
dotnet ef migrations add <Name>          # Add EF Core migration
dotnet ef database update                # Apply migrations
```

### Frontend (Next.js)
```bash
cd client && npm run dev                  # Dev server on http://localhost:3001
npm run build                             # Production build
npm run lint                              # ESLint
```

### Integration Tests
```bash
cd POS-System.Integration.Tests && dotnet test
dotnet test --collect:"XPlat Code Coverage"   # With coverage
```

## Architecture

### Backend — Clean/Layered Architecture

Five C# projects in `POS-System.sln`:

- **`POS-System.Api`** — Controllers, middleware, DI registration, Swagger. Entry point.
- **`POS-System.Business`** — Services, DTOs, AutoMapper profiles, FluentValidation validators, Stripe/email/SMS integrations. All business logic lives here.
- **`POS-System.Data`** — EF Core `ApplicationDbContext`, repository pattern, Unit of Work, DB seeder, migrations. Uses PostgreSQL via Npgsql.
- **`POS-System.Domain`** — Entity models only (Cart, Product, Service, Transaction, Employee, Tax, Discount, GiftCard, TimeSlot, etc.).
- **`POS-System.Common`** — Shared enums, constants, custom exceptions, `ErrorDetails`.

**Request flow:** Controller → Service (Business) → Repository (Data) → PostgreSQL

**Auth:** JWT Bearer with 20+ claims-based authorization policies (e.g., `TransactionRead`, `ItemWrite`). Policies are defined in `POS-System.Api` DI setup.

**Middleware:** Global exception handler + request/response logger (writes to `Events.log` / `Exceptions.log`).

### Frontend — Next.js App Router

```
client/src/
  app/           # Next.js App Router pages (dashboard/, login/)
  api/           # Class-based API clients (one per domain, e.g. CartApi, ProductApi)
  components/
    layouts/     # Layout wrappers
    pages/       # Full page components
    shared/      # Reusable UI primitives
    specialized/ # Domain-specific components
  hooks/         # Custom data-fetching hooks (*.hook.ts) with client-side caching
  types/         # TypeScript types (models.ts, auth.ts, payment.ts, components/)
  utils/         # Fetch wrapper, JWT helpers
  mappers/       # API response → frontend model transformations
  constants/     # API URLs, routes, nav config
  middleware.ts  # JWT auth guard — redirects unauthenticated users to /login
```

**Auth:** `src/middleware.ts` verifies JWT on every protected route. Disable via `REQUIRE_AUTH=false` in `.env`.

**API calls:** All go through a centralized fetch utility in `utils/`. API classes in `api/` group endpoints by domain.

**State:** No global store (no Redux/Zustand). Each feature uses a custom hook for data fetching.

### Database

PostgreSQL on `localhost:5432`. Connection string and JWT secret are in `POS-System.Api/appsettings.Development.json`. EF Core migrations are in `POS-System.Data/Migrations/`.

## Key Integrations

- **Stripe** — Payment processing. Keys in `appsettings.Development.json` (backend) and `.env` (frontend).
- **AWS SNS** — SMS notifications.
- **MailKit** — Email sending.

## Integration Tests

Tests live in `POS-System.Integration.Tests`. The project uses a `WebApplicationFactory` pattern against the real ASP.NET app. Coverage reports go to `TestResults/` (Cobertura XML format).
