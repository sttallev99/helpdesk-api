# HelpDesk - Support Ticket System

A production-style support ticket platform built with ASP.NET Core Web API, EF Core, and SQL Server. Customers submit tickets, support agents triage and resolve them, and administrators manage users, categories, and reporting.

## Key features

- JWT-based authentication on top of ASP.NET Core Identity, with three roles: **Customer**, **Agent**, **Admin**.
- Full ticket lifecycle (`Open -> InProgress -> WaitingForCustomer -> Resolved -> Closed`, plus reopening) enforced by domain rules, not controller-level `if` statements.
- Public replies and agent/admin-only internal notes, with server-side enforcement so a customer can never see an internal note.
- Full audit trail: every status, assignment, and detail change is recorded in `TicketHistory`.
- Optimistic concurrency via SQL Server `rowversion`, so two agents claiming the same ticket at once produces exactly one winner and one `409 Conflict`.
- Admin reporting endpoint (ticket counts by status, unassigned/overdue counts, tickets-per-agent, average resolution time computed from history).
- Consistent `ProblemDetails` error responses (400/401/403/404/409/500) from a single exception-handling middleware.

## Architecture

Layered architecture, not microservices - enough separation of concerns to be interview-legible without deployment complexity.

```
HelpDesk.Api             Controllers, JWT/Identity wiring, Swagger, exception-to-ProblemDetails mapping, Program.cs
HelpDesk.Application      MediatR commands/queries, FluentValidation validators, DTOs, orchestration (no EF/ASP.NET Core types)
HelpDesk.Domain           Entities, enums, state-transition rules, domain exceptions (no dependencies on anything else)
HelpDesk.Infrastructure   EF Core DbContext + configurations, ASP.NET Core Identity, JWT issuance, migrations
```

Dependency direction: `Api -> Application, Infrastructure -> Domain`. Application and Domain never reference EF Core, ASP.NET Core Identity, or ASP.NET Core itself (aside from `Microsoft.EntityFrameworkCore`'s `IQueryable` async extensions, used for pagination in Application).

Request flow for a mutating operation (e.g. claiming a ticket):

1. `TicketsController` receives the HTTP request and sends a MediatR command.
2. The command handler (Application) loads the `Ticket` aggregate and calls a domain method (`ticket.Claim(agentId)`).
3. The `Ticket` entity enforces the state-transition rules and appends a `TicketHistory` entry - impossible to change status without also recording why.
4. `SaveChangesAsync` persists the ticket and its new history entry in one transaction; a `DbUpdateConcurrencyException` (rowversion mismatch) is translated into a `ConcurrencyException` -> `409 Conflict`.
5. The controller returns the mapped DTO.

### Repository layout

```
HelpDesk/
├── src/
│   ├── HelpDesk.Api/
│   │   ├── Controllers/        AuthController, TicketsController, CategoriesController, AdminController
│   │   ├── Contracts/          Request/response records (thin, map 1:1 to MediatR commands)
│   │   ├── Middleware/         ApiExceptionHandler (IExceptionHandler -> ProblemDetails)
│   │   └── Program.cs
│   ├── HelpDesk.Application/
│   │   ├── Tickets/            CreateTicket, GetTicket, ListTickets, UpdateTicket, ClaimTicket, AssignTicket,
│   │   │                       ChangeTicketStatus, GetTicketHistory (one command/query + validator + handler per folder)
│   │   ├── Comments/            AddComment, AddInternalNote, ListComments
│   │   ├── Categories/, Users/, Reports/
│   │   └── Common/              Interfaces (IApplicationDbContext, ICurrentUserService, ...), exceptions, pagination, MediatR validation pipeline
│   ├── HelpDesk.Domain/
│   │   ├── Entities/            Ticket (aggregate root), TicketComment, TicketHistory, Category
│   │   ├── Enums/                TicketStatus, TicketPriority
│   │   └── Exceptions/
│   └── HelpDesk.Infrastructure/
│       ├── Identity/             ApplicationUser, JwtSettings, JwtTokenService
│       ├── Persistence/          ApplicationDbContext, EF configurations, migrations, DB seeder
│       └── Services/              CurrentUserService, TicketNumberGenerator, UserDirectory
├── tests/
│   ├── HelpDesk.UnitTests/        Domain state-transition rules + FluentValidation validators (no DB, no HTTP)
│   └── HelpDesk.IntegrationTests/ Full HTTP pipeline via WebApplicationFactory against a throwaway LocalDB database
├── docker-compose.yml             SQL Server container for local dev (see note below)
└── HelpDesk.slnx
```

## Database design

```
ApplicationUser (Identity)  1 ──< Ticket (as Customer)
ApplicationUser (Identity)  1 ──< Ticket (as AssignedAgent, nullable)
Category                    1 ──< Ticket
Ticket                      1 ──< TicketComment   (IsInternal flags agent/admin-only notes)
Ticket                      1 ──< TicketHistory   (append-only; Field/OldValue/NewValue/ChangedBy/ChangedAt)
```

Notable choices:

- `Ticket.TicketNumber` is generated from a SQL Server `SEQUENCE` (`TicketNumberSequence`), formatted as `TCK-000123`, with a unique index - safe under concurrent ticket creation.
- `Ticket.RowVersion` (`rowversion`/`timestamp`) is the optimistic-concurrency token used for claim/assign/status races.
- Indexes on `Status`, `Priority`, `AssignedAgentId`, `CreatedAt` to support the list/filter endpoint.
- All timestamps are stored and compared in UTC.
- Guid primary keys are explicitly `ValueGeneratedNever()` (app-assigned via `Guid.NewGuid()` inside domain factory methods) - see **Technical decisions** below for why this matters.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (ships with Visual Studio) **or** Docker Desktop (for the `sqlserver` service in `docker-compose.yml`)

## Setup

```bash
# 1. Restore
dotnet restore

# 2. Configure secrets (never commit these - they go in user secrets, not appsettings.json)
cd src/HelpDesk.Api
dotnet user-secrets init
dotnet user-secrets set "Jwt:SigningKey" "<a long random string, 32+ bytes>"
dotnet user-secrets set "SeedAdmin:Email" "admin@helpdesk.local"
dotnet user-secrets set "SeedAdmin:Password" "<a password meeting Identity's policy>"
cd ../..

# 3. Apply migrations (LocalDB connection string lives in appsettings.Development.json)
dotnet tool restore
dotnet dotnet-ef database update \
  --project src/HelpDesk.Infrastructure/HelpDesk.Infrastructure.csproj \
  --startup-project src/HelpDesk.Api/HelpDesk.Api.csproj

# 4. Run
dotnet run --project src/HelpDesk.Api/HelpDesk.Api.csproj --launch-profile http
```

Swagger UI is available at `http://localhost:5146/swagger` in the Development environment. On first run, the app seeds the `Customer`/`Agent`/`Admin` roles, four starter categories, and an admin account using the `SeedAdmin:*` secrets above.

### Using Docker for SQL Server instead of LocalDB

`docker-compose.yml` provisions a SQL Server container:

```bash
cp .env.example .env   # fill in MSSQL_SA_PASSWORD
docker compose up -d
```

Then update `ConnectionStrings:DefaultConnection` (e.g. via `dotnet user-secrets set` or an environment variable) to point at `localhost,1433` with the `sa` credentials from `.env`, and re-run step 3 above.

> **Note:** this repository was built in an environment without Docker installed, so the Docker path is written to the same contract LocalDB uses (standard SQL Server connection string) but has not been exercised end-to-end here. LocalDB **was** fully verified, including migrations, seeding, and the full test suite.

### Environment variables / configuration keys

| Key | Where it lives | Purpose |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `appsettings.Development.json` (LocalDB) or user secrets/env var | SQL Server connection string |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | Non-sensitive JWT settings |
| `Jwt:SigningKey` | **user secrets only** | HMAC-SHA256 signing key - never committed |
| `SeedAdmin:Email`, `SeedAdmin:Password` | **user secrets only** | Dev admin account seeded on first run |

### Web client

A Next.js front end for this API lives in [`../../client`](../../client). It talks to the API only from its server side (Server Components and Server Actions, with the JWT kept in an httpOnly cookie), so no CORS configuration is needed here. It expects the API on `http://localhost:5146` (the `http` launch profile); from the client folder, `npm run dev:all` starts both. See the client's README for details.

An Angular client lives in [`helpdesk-client`](https://github.com/sttallev99/helpdesk-client). It calls the API from the browser via the Angular dev server's proxy (`/api` -> `http://localhost:5146`), so it also needs no CORS configuration here. Clone it next to this repo (see its README for the expected folder layout); from the client folder, `npm run dev:all` starts the API and the client (`http://localhost:4200`).

## Authentication

- `POST /api/v1/auth/register` - self-service registration; always creates a **Customer**.
- `POST /api/v1/auth/login` - returns a JWT access token.
- `GET /api/v1/auth/me` - current user's profile and roles.
- Promoting a user to **Agent** or **Admin** is an admin-only action (`PATCH /api/v1/admin/users/{id}/role`) - there is no self-service way to become staff.
- In Swagger, click **Authorize** and paste the raw token (no `Bearer ` prefix needed - Swashbuckle adds it).

## API endpoints

| Method | Endpoint | Roles | Notes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anonymous | |
| POST | `/api/v1/auth/login` | anonymous | |
| GET | `/api/v1/auth/me` | any authenticated | |
| POST | `/api/v1/tickets` | Customer | 201 |
| GET | `/api/v1/tickets` | any authenticated | filters: `status`, `priority`, `categoryId`, `unassignedOnly`, `assignedToMe`; paginated, max page size 100 |
| GET | `/api/v1/tickets/{id}` | owner / Agent / Admin | 404 if a customer requests another customer's ticket |
| PATCH | `/api/v1/tickets/{id}` | owner / Agent / Admin | title/description/category only; blocked once Resolved/Closed |
| POST | `/api/v1/tickets/{id}/claim` | Agent | 409 if already claimed (race-safe) |
| POST | `/api/v1/tickets/{id}/assign` | Admin | assigns a specific agent |
| POST | `/api/v1/tickets/{id}/status` | owner (reopen only) / Agent / Admin | 409 on an illegal transition |
| GET | `/api/v1/tickets/{id}/history` | owner / Agent / Admin | |
| GET | `/api/v1/tickets/{id}/comments` | owner / Agent / Admin | internal notes hidden from customers |
| POST | `/api/v1/tickets/{id}/comments` | owner / Agent / Admin | public reply |
| POST | `/api/v1/tickets/{id}/internal-notes` | Agent / Admin | |
| GET | `/api/v1/categories` | any authenticated | active categories only (supports the "create ticket" form) |
| GET | `/api/v1/admin/users` | Admin | |
| PATCH | `/api/v1/admin/users/{id}/role` | Admin | |
| GET / POST / PATCH | `/api/v1/admin/categories` | Admin | includes inactive categories |
| GET | `/api/v1/admin/reports/summary` | Admin | |

### Example requests

```bash
# Register a customer
curl -X POST http://localhost:5146/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"cathy@example.com","password":"Customer123!","fullName":"Cathy Customer"}'

# Create a ticket (replace $TOKEN and $CATEGORY_ID)
curl -X POST http://localhost:5146/api/v1/tickets \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Printer on fire","description":"Smoke from the office printer.","priority":"High","categoryId":"'$CATEGORY_ID'"}'
```

## Ticket lifecycle

```
Open --------> InProgress --------> WaitingForCustomer
 ^  \              |   ^                    |
 |   \---> Resolved <---+<-------------------+
 |              |
 +----- Closed -+
```

- Customers may only move a ticket back to **Open** (reopen) - any other status change attempt by a customer returns `403`.
- Agents/Admins may drive any transition allowed by the table above; illegal ones (e.g. `Closed -> InProgress`) return `409`.
- A customer's public reply while a ticket is `WaitingForCustomer` automatically reopens it.

## Running tests

```bash
# Unit tests - Domain rules + validators, no DB or HTTP involved
dotnet test tests/HelpDesk.UnitTests/HelpDesk.UnitTests.csproj

# Integration tests - spins up the real Api pipeline (WebApplicationFactory) against a
# throwaway LocalDB database per test run, created on start and dropped on teardown.
# Requires LocalDB to be available (see Prerequisites).
dotnet test tests/HelpDesk.IntegrationTests/HelpDesk.IntegrationTests.csproj
```

Integration coverage includes the plan's essential scenarios: ticket creation (201), cross-customer access denial (404), internal notes hidden from the owning customer, illegal status transitions (409), a customer blocked from setting an arbitrary status (403), and two agents claiming the same ticket concurrently (exactly one `200`, one `409`, and exactly one set of history entries - proving the failed attempt left no partial write).

## Technical decisions, trade-offs, and known limitations

- **Resource-ownership authorization lives in Application handlers, not ASP.NET Core's `IAuthorizationService`.** Role-level checks (`[Authorize(Roles = ...)]`) are on controllers; "does this customer own this specific ticket" is checked in the handler after loading it (`TicketAccessGuard`), since the decision is data-dependent. This is simpler than wiring up resource-based authorization handlers for a project this size, at the cost of the rule living in a different place than role checks.
- **EF Core + client-generated Guid keys on entities added via a tracked aggregate's private collection requires `ValueGeneratedNever()`.** Without it, EF Core's change tracker, when a `Ticket` is loaded (tracked) and a new `TicketHistory`/`TicketComment` is appended to its backing-field collection, treats the new row as an *update* to a non-existent row instead of an insert (because Guid properties default to `ValueGeneratedOnAdd`, and EF assumes a non-default key on a row reached via navigation fixup from an already-tracked parent means "this already exists"). This cost real debugging time during development and is now covered by an integration test (`ClaimTicket_TwoAgentsClaimSimultaneously_OnlyOneSucceeds`, which also asserts the history count) so a regression would be caught immediately.
- **`SELECT NEXT VALUE FOR ...` cannot be used inside an EF Core `SqlQuery<T>`** (SQL Server forbids it inside the subquery EF wraps scalar raw queries in). `TicketNumberGenerator` issues it via a raw ADO.NET command on the context's connection instead.
- **No attachments, background notification worker, or CI pipeline.** All marked optional/advanced in the plan; out of scope for this pass so the core workflow could be fully tested end-to-end first.
- **Reporting metrics are intentionally simple**: "overdue" is a flat 72-hour-since-creation threshold (not SLA-per-priority), and average resolution time is computed from the first `TicketHistory` row where `Status` moved to `Resolved`, not from `Ticket.UpdatedAt` (which changes on every edit, not just resolution).
- **`GET /api/v1/categories`** (any authenticated user, active-only) isn't in the plan's original endpoint table; it was added so a customer's "create ticket" form has somewhere to source valid category IDs without needing admin access.
