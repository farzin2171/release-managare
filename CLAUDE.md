# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**Repository Release Management Platform** — a full-stack web app that helps engineering teams track changes across Azure DevOps repositories, reconcile shipped commits against Jira tickets, generate release notes, and publish them to Confluence.

Full specifications are in [docs/](docs/):
- [01-constitution.md](docs/01-constitution.md) — 9 architectural principles (authoritative)
- [02-specify.md](docs/02-specify.md) — feature requirements
- [03-plan.md](docs/03-plan.md) — technical implementation plan (DB schema, interfaces, API endpoints)
- [04-tasks-guidance.md](docs/04-tasks-guidance.md) — 11 vertical-slice milestones and build order

**Status**: Spec-complete, implementation not yet started. Source directories (`backend/`, `frontend/`) do not exist yet.

<!-- SPECKIT START -->
**Active feature plan**: [specs/002-latest-tag-selection/plan.md](specs/002-latest-tag-selection/plan.md)
<!-- SPECKIT END -->

## Tech Stack

**Backend**: .NET 10, ASP.NET Core Web API, EF Core 10 + SQLite (WAL), Serilog, FluentValidation, Mapster, Polly, HandlebarsDotNet, BCrypt.Net-Next, Swashbuckle, Markdig

**Frontend**: React 18 + TypeScript + Vite, Tailwind CSS, shadcn/ui, TanStack Query, Zustand, React Hook Form + Zod, React Router v6, openapi-typescript (generated API client)

**External integrations**: Azure DevOps (v1 Git provider), Confluence Cloud REST v2, Jira Cloud REST v3

## Commands

Once source directories exist:

```powershell
# Backend
dotnet build backend/src
dotnet test backend/tests
dotnet test --filter "FullyQualifiedName~ConventionalCommitParser"  # single test class
dotnet ef migrations add <Name> --project backend/src/RepoManager.Infrastructure --startup-project backend/src/RepoManager.Api
dotnet ef database update --project backend/src/RepoManager.Infrastructure --startup-project backend/src/RepoManager.Api

# Frontend
npm install           # from frontend/
npm run dev
npm run build
npm run lint
```

## Architecture

### Backend — 4-project layered solution (no CQRS, no MediatR)

```
RepoManager.Domain          entities, enums, value objects — no external deps
RepoManager.Application     service interfaces, DTOs (records), validators, exceptions
RepoManager.Infrastructure  EF Core, service implementations, external API clients
RepoManager.Api             controllers, middleware, DI, OpenAPI
```

Dependencies flow strictly downward: Domain ← Application ← Infrastructure ← Api.

Controllers inject service interfaces directly: `await _projects.CreateAsync(dto, ct)`.

**One service per aggregate**: `ProjectService`, `ReleaseService`, `RepositoryService`, `GitProviderConnectionService`, `CommitSyncService`, `ConventionalCommitParser`, `ReleaseReconciliationService`, `ConfluencePublisher`, `JiraService`, `AuthService`.

**Key abstractions** (in Application layer):
- `IGitProvider` / `IGitProviderFactory` — v1 ships `AzureDevOpsGitProvider`; GitHub/GitLab/Bitbucket added via new implementations only
- `IConfluencePublisher` — Confluence Cloud REST v2
- `IJiraService` — Jira Cloud REST v3 via typed `HttpClient` with Polly (3 retries, exponential backoff on 429/5xx)
- `IConventionalCommitParser` — pure C# regex, TDD-first

### Service conventions

- Every async method takes `CancellationToken ct = default` as the last parameter
- Services accept and return DTOs (record types), never EF entities
- Validate with `await _validator.ValidateAndThrowAsync(dto, ct)` — never inline validation
- Throw typed exceptions (`NotFoundException`, `ConflictException`, `ValidationException`, `ExternalServiceException`); the global `IExceptionHandler` maps them to RFC 7807 ProblemDetails
- Cross-aggregate writes use explicit `BeginTransactionAsync()`
- No service file over 300 lines, no method over 30 lines — split by use-case group

### Frontend structure

```
frontend/src/
  features/         one folder per domain (auth, projects, repositories, releases, reconciliation, settings)
  components/ui/    shadcn components only — no custom primitives that duplicate shadcn
  lib/              generated API client (openapi-typescript), shared utils
  routes/           React Router v6 route definitions with role guards
```

State: TanStack Query for server state, Zustand for auth and UI state.

### Database

SQLite at `./backend/data/repomanager.db`, WAL mode enabled. EF Core migrations auto-applied on startup in development. All external secrets (PATs, API tokens) encrypted at rest via `IDataProtectionProvider`.

### Auth

JWT bearer, 8-hour expiry, refresh-token rotation. Roles: `Admin`, `Viewer`. All endpoints require `[Authorize]`; write/admin endpoints add `[Authorize(Roles = "Admin")]`. Exceptions: `/health/*` and `/auth/login`. First-run bootstrap via `/api/v1/auth/setup` (auto-disables after one use).

## Build Order

Implement milestone-by-milestone per [04-tasks-guidance.md](docs/04-tasks-guidance.md). Each milestone ends with its smoke test passing before moving on:

1. Foundation (solution, EF Core, auth, Serilog, health endpoints, login UI)
2. Git provider integration (AzureDevOps, PAT encryption, test-connection)
3. Repository sync
4. Logical projects
5. Conventional commit parsing + ticket aggregation ← TDD required here
6. Project change visibility
7. Release creation and notes generation (Handlebars templates)
8. Confluence publishing (Markdown → Confluence storage format via Markdig)
9. Jira integration foundation
10. Reconciliation (Jira vs Git set diff, subtask matching)
11. Hardening (users UI, templates UI, audit logging, Dockerfile)

## Key Constraints

- No CQRS, no MediatR — controllers call service interfaces directly
- No premature abstraction — only `IGitProvider`, `IConfluencePublisher`, `IJiraService` are intentionally extensible
- Frontend uses only shadcn/ui — never build custom component primitives that shadow shadcn components
- External APIs are never called from request handlers — sync jobs cache data in SQLite first
- Sync jobs must be idempotent
