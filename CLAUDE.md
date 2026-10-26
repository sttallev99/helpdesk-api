# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

HelpDesk - a support ticket system (ASP.NET Core Web API, EF Core, SQL Server, ASP.NET Core Identity + JWT, MediatR, FluentValidation). See `README.md` for feature overview, API endpoint table, ticket lifecycle, and documented trade-offs - read it before making architectural changes, it covers the "why" behind several non-obvious decisions.

## Commands

Run from the repository root (`HelpDesk.slnx`).

- Build: `dotnet build HelpDesk.slnx`
- Run the API: `dotnet run --project src/HelpDesk.Api/HelpDesk.Api.csproj --launch-profile http` (Swagger at `http://localhost:5146/swagger` in Development)
- Unit tests (no DB): `dotnet test tests/HelpDesk.UnitTests/HelpDesk.UnitTests.csproj`
- Integration tests (needs LocalDB - creates and drops a throwaway database per run): `dotnet test tests/HelpDesk.IntegrationTests/HelpDesk.IntegrationTests.csproj`
- Single test: add `--filter FullyQualifiedName~<Name>` to either `dotnet test` command above
- EF Core migrations: `dotnet dotnet-ef migrations add <Name> --project src/HelpDesk.Infrastructure/HelpDesk.Infrastructure.csproj --startup-project src/HelpDesk.Api/HelpDesk.Api.csproj --output-dir Persistence/Migrations`
- Apply migrations: same command with `database update` instead of `migrations add <Name>`
- `dotnet-ef` is a local tool (`dotnet-tools.json`) - run `dotnet tool restore` once after cloning if `dotnet dotnet-ef` isn't found

User secrets required before the API will start (`cd src/HelpDesk.Api && dotnet user-secrets set ...`): `Jwt:SigningKey`, `SeedAdmin:Email`, `SeedAdmin:Password`. See README's Setup section for the full sequence.

## Architecture

Layered: `HelpDesk.Api -> HelpDesk.Application, HelpDesk.Infrastructure -> HelpDesk.Domain`. Application and Domain do not reference EF Core, ASP.NET Core Identity, or ASP.NET Core (Application references the `Microsoft.EntityFrameworkCore` package only for `IQueryable` async/pagination extensions - not for anything SQL Server-specific).

- **Domain** (`src/HelpDesk.Domain`): `Ticket` is the aggregate root and owns all state-transition rules (`Claim`, `Assign`, `ChangeStatus`, `AddComment`, `UpdateDetails`) - every mutation appends a `TicketHistory` entry from inside the entity itself, so it's structurally impossible to change a ticket without recording why. Allowed status transitions are a static table in `Ticket.cs`, not scattered `if` checks.
- **Application** (`src/HelpDesk.Application`): one folder per use case (e.g. `Tickets/ClaimTicket/`) containing the MediatR command/query, its FluentValidation validator, and its handler in a single file. `TicketAccessGuard` centralizes the "customer can only see their own ticket" rule (returns 404, not 403, to avoid confirming existence). `IApplicationDbContext`, `ICurrentUserService`, `IJwtTokenService`, `ITicketNumberGenerator`, `IUserDirectory` are the seams Infrastructure implements.
- **Infrastructure** (`src/HelpDesk.Infrastructure`): `ApplicationDbContext` (`IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`), EF configurations in `Persistence/Configurations/`, JWT issuance, and `ApplicationDbContextInitializer` (migrate + seed roles/categories/dev admin, called from `Program.cs` only when `IsDevelopment()`).
- **Api** (`src/HelpDesk.Api`): thin controllers that just send a MediatR command/query and map the result; `ApiExceptionHandler` (`IExceptionHandler`) is the single place that turns exceptions into `ProblemDetails` (`ValidationException`->400, `NotFoundException`->404, `ForbiddenAccessException`->403, `DomainException`/`ConcurrencyException`->409).

### Non-obvious EF Core constraint worth knowing before touching `Ticket`/`TicketComment`/`TicketHistory`

These entities use app-assigned `Guid` keys (`Guid.NewGuid()` in domain factory methods) and are explicitly configured `.ValueGeneratedNever()` in their EF configurations. If you load a tracked `Ticket` without `.Include(t => t.History).Include(t => t.Comments)` and then append to those collections via a domain method, EF Core's change tracker will try to `UPDATE` the new rows instead of `INSERT`-ing them (it assumes a non-default key reached via navigation fixup from an already-tracked parent already exists in the DB), producing a wrong-affected-row-count `DbUpdateConcurrencyException`. Every Application handler that mutates a ticket's history/comments already does this `.Include(...)` - keep doing it if you add a new one. Covered by `ClaimTicket_TwoAgentsClaimSimultaneously_OnlyOneSucceeds` in the integration tests.

### Role-based vs resource-based authorization

`[Authorize(Roles = ...)]` on controllers handles "can this role call this endpoint at all." Whether a *specific* ticket is visible/editable by the caller (ownership) is checked inside the Application handler via `ICurrentUserService` + `TicketAccessGuard`, not through ASP.NET Core's `IAuthorizationService`/resource-based handlers. Keep new ticket-scoped write operations consistent with this split.
