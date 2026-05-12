# Implementation Plan: Repository Release Management Platform

**Branch**: `001-release-mgmt-platform` | **Date**: 2026-05-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-release-mgmt-platform/spec.md`

## Summary

Build a full-stack web application that lets engineering teams track unreleased changes across logical projects spanning multiple Azure DevOps repositories, reconcile shipped commits against Jira fix versions, generate structured release notes via Handlebars templates, and publish them to Confluence — with JWT-authenticated role-based access for Admins and Viewers.

The backend is a 4-project layered .NET 10 solution (Domain → Application → Infrastructure → Api) backed by SQLite with WAL mode. The frontend is a React 18 + TypeScript SPA generated from the OpenAPI spec. Implementation follows 11 vertical-slice milestones (see `docs/04-tasks-guidance.md`).

## Technical Context

**Language/Version**: C# 13 / .NET 10; TypeScript 5.x  
**Primary Dependencies**: ASP.NET Core Web API, EF Core 10, FluentValidation, Mapster, Serilog, Polly, Swashbuckle, HandlebarsDotNet, Markdig, BCrypt.Net-Next; React 18, Vite, TanStack Query, Zustand, React Hook Form + Zod  
**Storage**: SQLite via `Microsoft.EntityFrameworkCore.Sqlite` (10.x), WAL mode, file at `./backend/data/repomanager.db`  
**Testing**: xunit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing (backend); Vitest (frontend unit)  
**Target Platform**: Docker container (Linux), accessed from a modern web browser  
**Project Type**: Full-stack web service (REST API + SPA)  
**Performance Goals**: Jira reconciliation for 20–50 tickets < 10 s (SC-004); release creation wizard < 5 min end-to-end (SC-002)  
**Constraints**: WCAG AA, HTTPS-only in production, < 10 s reconciliation, single-tenant (one organisation)  
**Scale/Scope**: Single organisation, up to ~100 repositories, ~50 Jira tickets per reconciliation run, two user roles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Layered Architecture, Not CQRS | ✅ PASS | 4-project solution, downward deps only, no MediatR |
| II | API-First Design | ✅ PASS | All features via `/api/v1`; openapi-typescript codegen; no hand-written client calls |
| III | Test-Driven Development | ✅ PASS | TDD mandatory for `IConventionalCommitParser`; integration tests against real SQLite; E2E for release + publish flow |
| IV | Security by Default | ✅ PASS | JWT on all endpoints except `/health/*` and `/auth/login`; `IDataProtectionProvider` for all secrets |
| V | Observability | ✅ PASS | Serilog request logging, `CorrelationIdMiddleware`, all external API calls logged with duration |
| VI | Simplicity Over Cleverness | ✅ PASS | No CQRS, vertical slices, 300/30 line limits |
| VII | Extensibility Where It Matters | ✅ PASS | Exactly 3 seams: `IGitProvider`, `IConfluencePublisher`, `IJiraService` |
| VIII | UX Standards | ✅ PASS | shadcn/ui only, loading + error states everywhere, confirmation dialogs for destructive actions, WCAG AA, dark mode |
| IX | Data Integrity | ✅ PASS | WAL enabled, explicit `BeginTransactionAsync()` for cross-aggregate writes, all syncs idempotent |

**Post-Phase-1 re-check**: All principles confirmed satisfied by data model and API contract design. No violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/001-release-mgmt-platform/
├── plan.md              # This file
├── research.md          # Phase 0 — key decisions and rationale
├── data-model.md        # Phase 1 — full entity schema and relationships
├── quickstart.md        # Phase 1 — developer onboarding
├── contracts/           # Phase 1 — API endpoints and service interfaces
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (repository root)

```text
backend/
├── src/
│   ├── RepoManager.Domain/
│   │   ├── Entities/
│   │   ├── Enums/
│   │   └── ValueObjects/
│   ├── RepoManager.Application/
│   │   ├── Common/Exceptions/
│   │   ├── Auth/
│   │   ├── Projects/
│   │   │   ├── IProjectService.cs
│   │   │   ├── Dtos/
│   │   │   └── Validators/
│   │   ├── Releases/
│   │   ├── Repositories/
│   │   ├── GitProviders/
│   │   ├── Confluence/
│   │   ├── Jira/
│   │   ├── Reconciliation/
│   │   ├── Commits/
│   │   └── Templates/
│   ├── RepoManager.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   ├── Auth/
│   │   ├── Projects/
│   │   ├── Releases/
│   │   ├── Repositories/
│   │   ├── GitProviders/
│   │   │   ├── AzureDevOpsGitProvider.cs
│   │   │   └── GitProviderFactory.cs
│   │   ├── Confluence/
│   │   │   └── ConfluencePublisher.cs
│   │   ├── Jira/
│   │   │   └── JiraService.cs
│   │   ├── Reconciliation/
│   │   │   └── ReleaseReconciliationService.cs
│   │   ├── Commits/
│   │   │   ├── ConventionalCommitParser.cs
│   │   │   └── CommitSyncService.cs
│   │   └── DependencyInjection.cs
│   └── RepoManager.Api/
│       ├── Controllers/
│       ├── Middleware/
│       │   ├── GlobalExceptionHandler.cs
│       │   └── CorrelationIdMiddleware.cs
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   ├── RepoManager.UnitTests/
│   └── RepoManager.IntegrationTests/
└── data/                   # SQLite file location (gitignored)

frontend/
├── src/
│   ├── features/
│   │   ├── auth/
│   │   ├── projects/
│   │   ├── repositories/
│   │   ├── releases/
│   │   ├── reconciliation/
│   │   └── settings/
│   │       ├── integrations/
│   │       ├── projects/
│   │       ├── repositories/
│   │       ├── templates/
│   │       └── users/
│   ├── components/ui/          # shadcn components only
│   ├── lib/                    # generated API client, shared utils
│   └── routes/                 # React Router v6 route definitions
└── tests/

docker-compose.yml
```

**Structure Decision**: Web application layout (Option 2). Backend and frontend are co-located in the same repository but built independently. The Docker Compose file runs both together for development. In production, a single container serves the backend API and the compiled frontend static assets.

## Complexity Tracking

> No constitution violations found. This section is intentionally empty.
