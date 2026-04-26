# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Full-stack Point of Sale (POS) system. Backend is ASP.NET Core 8 REST API; frontend is Next.js 15 with React 19 and TypeScript.

## Running the Project

**Backend API** (port 3000):
```bash
cd POS-System.Api
dotnet run
```
Swagger UI available at `http://localhost:3000/swagger/index.html` in development.

**Frontend** (port 3001):
```bash
cd client
npm run dev
```

## Build, Lint & Test Commands

**Backend:**
```bash
dotnet build                                          # Build solution
dotnet test                                           # Run all integration tests
dotnet test --filter "FullyQualifiedName~MethodName"  # Run a single test
```

**Frontend (from `client/`):**
```bash
npm run build    # Production build
npm run lint     # ESLint (next/core-web-vitals + next/typescript)
```

Integration tests live in `POS-System.IntegrationTests/` and use xUnit + `Microsoft.AspNetCore.Mvc.Testing`. Coverage is collected via coverlet using `coverage.api.runsettings`.

## Architecture

### Backend — Layered .NET 8 Solution

| Project | Responsibility |
|---|---|
| `POS-System.Api` | Controllers, middleware, DI composition root |
| `POS-System.Business` | Services, DTOs, AutoMapper profiles, FluentValidation validators |
| `POS-System.Data` | EF Core DbContext, repositories, Identity, PostgreSQL migrations |
| `POS-System.Domain` | Domain entity classes (no business logic) |
| `POS-System.Common` | Shared constants and utilities |
| `POS-System.IntegrationTests` | End-to-end API tests using `TestWebApplicationFactory` + `TestAuthHandler` |

The API is a thin layer — controllers delegate to `Business` services which call `Data` repositories. AutoMapper maps between domain entities and DTOs. FluentValidation validators are registered per-request.

External integrations: Stripe (payments), AWS SNS (SMS), MailKit (email).

### Frontend — Next.js App Router

Source lives in `client/src/`:

| Directory | Responsibility |
|---|---|
| `app/` | Next.js pages and route layouts |
| `components/` | Shared React components |
| `api/` | Typed API client functions (calls to backend on port 3000) |
| `hooks/` | Custom React hooks |
| `types/` | TypeScript interfaces and types |
| `mappers/` | Data transformation between API responses and UI models |
| `constants/` | App-wide configuration constants |

Auth uses `jose` (JWT) with `cookies-next` for cookie management. Styling uses TailwindCSS and SASS.

## Key Conventions

- **Validation**: FluentValidation validators in `POS-System.Business/Validators/` — add one per new resource.
- **Mapping**: AutoMapper profiles in `POS-System.Business/Mapping/` — define entity↔DTO maps there.
- **Repositories**: Data access goes through repository interfaces in `POS-System.Data/Repositories/`; services depend on these interfaces.
- **Integration tests**: `IntegrationScenarioTests.cs` tests HTTP endpoints end-to-end; `TestAuthHandler` bypasses real auth. New endpoints should be tested here.
- **Database changes**: Add EF Core migrations with `dotnet ef migrations add <Name> --project POS-System.Data --startup-project POS-System.Api`.
