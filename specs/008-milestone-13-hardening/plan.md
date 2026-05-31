# Implementation Plan: Milestone 13 — Security, Service Ownership & UX Hardening

**Branch**: `009-milestone-13-hardening` | **Date**: 2026-05-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/008-milestone-13-hardening/spec.md`

## Summary

Five targeted hardening changes across security, data enrichment, templating, session management, and release lifecycle: (A) a pre-shared API key protects the first-run admin setup endpoint from unauthorized takeover; (B) a nullable `ServiceOwner` field on `Repository` flows through to all templates and UI; (C) a seeded, read-only "Release Summary (Default)" Handlebars template with per-repo ownership rows is auto-bound to every new project; (D) the frontend transparently renews JWT sessions via a shared in-flight promise and proactive timer initialised on every app load; (E) Draft releases can be deleted from both the list and detail views, with Admin-only visibility and race-condition handling.

Key technical additions: `SetupKeyStartupValidator` (IHostedService), `SetupKeyAuthorizationFilter` (IAsyncActionFilter), 2 EF Core migrations (`AddColumn_Repositories_ServiceOwner`, `AddColumn_Templates_IsSystem` + InsertData seed), `RepoSummaryContext` DTO + `ReleaseRenderContext.Repositories` extension, `ITemplateService.CloneAsync` with auto-increment naming, httpOnly cookie on `POST /auth/refresh`, 401-intercept → refresh → retry-once Axios interceptor with shared promise, proactive refresh timer in Zustand auth store, and 4 frontend component additions.

## Technical Context

**Language/Version**: .NET 10 (backend), React 18 + TypeScript (frontend)
**Primary Dependencies**: EF Core 10 + SQLite, ASP.NET Core Web API, HandlebarsDotNet, FluentValidation, Mapster, Serilog, BCrypt.Net-Next, TanStack Query, Zustand, shadcn/ui, React Hook Form + Zod, openapi-typescript
**Storage**: SQLite WAL — `./data/repomanager.db`
**Testing**: xunit + FluentAssertions + Moq (unit), SQLite per-test file (integration), Playwright for session-renewal E2E
**Target Platform**: Web — browser (React SPA) + Linux server (.NET)
**Project Type**: Full-stack web application
**Performance Goals**: Delete draft fade animation completes within 2 s; session renewal is transparent (user perceives 0 interruption); template preview for a 3-repo release renders in < 2 s
**Constraints**: `ServiceOwner` max 120 chars; setup key min 32 chars; proactive refresh at `exp - 2 min`; refresh token in httpOnly cookie only
**Scale/Scope**: Single-server deployment; tens of concurrent users; up to ~50 repositories per installation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked post-design — marks below reflect final state.*

| Principle | Check | Notes |
|-----------|-------|-------|
| I. Layered Architecture | PASS | Domain: 2 entity field additions. Application: new DTO (`RepoSummaryContext`), extended `ReleaseRenderContext`, updated DTOs + validators, `ITemplateService.CloneAsync`. Infrastructure: 2 migrations, updated service impls, seed data constant. Api: `SetupKeyStartupValidator`, `SetupKeyAuthorizationFilter`, controller method additions. No upward dependencies. |
| II. API-First Design | PASS | 8 modified/new endpoints fully specified in `contracts/api-endpoints.md`. Frontend uses generated client; the 401 interceptor operates on the response layer of the generated client, never bypasses it. OpenAPI regenerated after backend changes. |
| III. TDD | PASS | `SetupKeyAuthorizationFilter` and `SetupKeyStartupValidator` unit tests written and confirmed failing before implementation (TDD gate at Step 1 of quickstart). Integration tests cover: setup endpoint auth scenarios, `ServiceOwner` round-trip, system template protection, `ReleaseRenderContext.Repositories` content. |
| IV. Security by Default | PASS | Setup key never logged (constant-time comparison, Serilog request log excludes `X-Setup-Key` header). Key absent → startup rejected before first request. Refresh token in httpOnly + Secure + SameSite=Strict cookie. `CloneAsync` + `ServiceOwner` update both require `[Authorize(Roles = "Admin")]`. |
| V. Observability | PASS | `SetupKeyStartupValidator` logs Fatal before stopping. `SetupKeyAuthorizationFilter` logs Warning on mismatch (without key value). `TemplateService.CloneAsync` logs with duration. Correlation ID propagated through all new code paths via existing middleware. |
| VI. Simplicity | PASS | Each new class is < 50 lines. `TemplateService` additions < 60 lines total. No CQRS, no MediatR. Frontend interceptor pattern is one module-level variable + one interceptor registration. |
| VII. Extensibility | PASS | No new `I*` interfaces. `SetupKeyStartupValidator` and `SetupKeyAuthorizationFilter` are concrete classes. `CloneAsync` is added to the existing `ITemplateService` — a minimal extension to an already-extensible seam. |
| VIII. UX Standards | PASS | Delete draft uses `AlertDialog` (shadcn, destructive variant) — mandatory confirmation for irreversible action. [System] badge uses shadcn `Badge`. Clone, kebab menu, and toast all use shadcn/ui components. Loading state shown on Clone and Delete actions. |
| IX. Data Integrity | PASS | `ServiceOwner` migration: nullable column, no backfill. `IsSystem` migration: column + `InsertData` seed in same `Up` method (atomic). Auto-bind system template on `Project.CreateAsync` wrapped in existing project-creation transaction. `CloneAsync` naming check + insert inside DB transaction (no duplicate-name race). |

**Violations**: None.

## Project Structure

### Documentation (this feature)

```text
specs/008-milestone-13-hardening/
├── plan.md                       ← this file
├── research.md                   ← Phase 0 complete
├── data-model.md                 ← Phase 1 complete
├── quickstart.md                 ← Phase 1 complete
├── contracts/
│   ├── api-endpoints.md          ← Phase 1 complete
│   └── service-interfaces.md     ← Phase 1 complete
├── checklists/
│   └── requirements.md           ← from /speckit-specify
└── tasks.md                      ← generated by /speckit-tasks (not yet)
```

### Source Code

```text
backend/
├── src/
│   ├── RepoManager.Domain/
│   │   └── Entities/
│   │       ├── Repository.cs                              ← add ServiceOwner property
│   │       └── ReleaseNoteTemplate.cs                     ← add IsSystem property
│   ├── RepoManager.Application/
│   │   ├── DTOs/
│   │   │   ├── RepositoryDto.cs                           ← add ServiceOwner
│   │   │   ├── UpdateRepositoryRequest.cs                 ← add ServiceOwner
│   │   │   ├── Templates/ReleaseNoteTemplateDto.cs        ← add IsSystem
│   │   │   └── Releases/
│   │   │       ├── RepoSummaryContext.cs                  ← NEW
│   │   │       └── ReleaseRenderContext.cs                ← extend with Repositories
│   │   ├── Services/
│   │   │   └── ITemplateService.cs                        ← add CloneAsync
│   │   └── Validators/
│   │       └── UpdateRepositoryRequestValidator.cs        ← add ServiceOwner max-length rule
│   ├── RepoManager.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── AppDbContext.cs                            ← add new DbSet configs if needed
│   │   │   ├── EntityConfigurations/
│   │   │   │   ├── RepositoryConfiguration.cs             ← configure ServiceOwner column
│   │   │   │   └── ReleaseNoteTemplateConfiguration.cs    ← configure IsSystem column
│   │   │   ├── SeedData/
│   │   │   │   └── ReleaseSummaryTemplateBody.cs          ← NEW (const template body)
│   │   │   └── Migrations/
│   │   │       ├── [ts]_AddColumn_Repositories_ServiceOwner/  ← NEW
│   │   │       └── [ts]_AddColumn_Templates_IsSystem/          ← NEW (+ InsertData seed)
│   │   └── Services/
│   │       ├── RepositoryService.cs                       ← pass through ServiceOwner
│   │       ├── TemplateService.cs                         ← guard Update/Delete, add CloneAsync
│   │       ├── TemplateRenderingService.cs                ← extend BuildContextAsync with Repositories
│   │       └── ProjectService.cs                          ← auto-bind system template on CreateAsync
│   └── RepoManager.Api/
│       ├── Controllers/
│       │   ├── AuthController.cs                          ← add filter, httpOnly cookie on refresh
│       │   ├── RepositoriesController.cs                  ← response/request pass-through
│       │   └── TemplatesController.cs                     ← add Clone endpoint
│       ├── Filters/
│       │   └── SetupKeyAuthorizationFilter.cs             ← NEW
│       └── StartupValidators/
│           └── SetupKeyStartupValidator.cs                ← NEW (IHostedService)
└── tests/
    ├── RepoManager.UnitTests/
    │   ├── Filters/
    │   │   └── SetupKeyAuthorizationFilterTests.cs        ← NEW (TDD first)
    │   └── StartupValidators/
    │       └── SetupKeyStartupValidatorTests.cs           ← NEW (TDD first)
    └── RepoManager.IntegrationTests/
        ├── Auth/
        │   └── SetupEndpointTests.cs                      ← NEW
        ├── Repositories/
        │   └── ServiceOwnerTests.cs                       ← NEW
        └── Templates/
            ├── TemplateSystemFlagTests.cs                 ← NEW
            └── TemplateRenderContextTests.cs              ← NEW

frontend/
└── src/
    ├── features/
    │   ├── auth/
    │   │   └── authStore.ts                               ← add scheduleRefresh + proactive timer
    │   ├── settings/
    │   │   ├── repositories/
    │   │   │   ├── RepositoriesTable.tsx                  ← add ServiceOwner column
    │   │   │   └── RepositoryEditPanel.tsx                ← add ServiceOwner Input field
    │   │   └── templates/
    │   │       └── TemplatesTable.tsx                     ← [System] badge, Clone button
    │   └── releases/
    │       ├── ReleasesTable.tsx                          ← kebab menu on Draft rows (Admin)
    │       └── ReleaseDetailPage.tsx                      ← Delete Draft button + 404 handling
    └── lib/
        └── apiClient.ts                                   ← 401-intercept + refresh + retry-once
```

## Phase 0: Research

**Status**: Complete — see [research.md](research.md).

Seven decisions resolved:
1. `IHostedService` is the correct mechanism for async startup validation with DB access.
2. Action filter (not middleware) is used for `X-Setup-Key` — endpoint-scoped, testable.
3. Serilog redaction: two layers (never pass key as structured param; exclude header from request enricher).
4. httpOnly cookie: `Secure + SameSite=Strict + Path=/api/v1/auth + MaxAge=30d`.
5. JWT exp decode: one-line `atob` + JSON.parse — no library needed.
6. Axios in-flight queue: module-level `refreshPromise` variable, `_retried` flag to prevent loops.
7. EF Core seeding: `migrationBuilder.InsertData` in migration `Up` — avoids `HasData` model-snapshot coupling.

## Phase 1: Design & Contracts

**Status**: Complete.

| Artifact | Status | Notes |
|----------|--------|-------|
| [data-model.md](data-model.md) | Complete | 2 entity field additions, 2 migrations, new `RepoSummaryContext` DTO, `ReleaseRenderContext` extension, clone naming algorithm, `IsSystem` state diagram |
| [contracts/api-endpoints.md](contracts/api-endpoints.md) | Complete | 7 modified + 1 new endpoint with full request/response shapes and error contracts |
| [contracts/service-interfaces.md](contracts/service-interfaces.md) | Complete | New `SetupKeyStartupValidator`, `SetupKeyAuthorizationFilter`, `ReleaseSummaryTemplateBody`, modified `ITemplateService`/`ITemplateRenderingService`/`IProjectService`, frontend auth store + interceptor contracts |
| [quickstart.md](quickstart.md) | Complete | 12-step implementation sequence + smoke test |

## Implementation Notes

### Backend sequence

1. **TDD gate** — write failing unit tests for `SetupKeyAuthorizationFilter` and `SetupKeyStartupValidator` before writing any filter/validator code. Confirm red.
2. **Domain** — add `ServiceOwner` to `Repository`, `IsSystem` to `ReleaseNoteTemplate`.
3. **Application layer** — new `RepoSummaryContext` DTO, extended `ReleaseRenderContext`, updated `RepositoryDto` / `UpdateRepositoryRequest` / `ReleaseNoteTemplateDto`, add `CloneAsync` to `ITemplateService`, update validator.
4. **EF Core config + migrations** — entity configurations, two migrations, `ReleaseSummaryTemplateBody` seed constant. Add `InsertData` to the `IsSystem` migration's `Up` method manually after generation.
5. **Service implementations** — `RepositoryService` pass-through, `TemplateService` (guard + clone), `TemplateRenderingService` (Repositories extension), `ProjectService` (auto-bind).
6. **Api layer** — `SetupKeyStartupValidator`, `SetupKeyAuthorizationFilter`, filter registration, httpOnly cookie in `AuthController.Refresh`, clone endpoint in `TemplatesController`.
7. **Integration tests green** — all new tests pass before switching to frontend.

### Frontend sequence

8. Regenerate OpenAPI client after backend is runnable.
9. `authStore.ts` — `scheduleRefresh`, proactive timer, hydration hook.
10. `apiClient.ts` — 401 interceptor with shared `refreshPromise`.
11. `RepositoryEditPanel.tsx` + `RepositoriesTable.tsx` — ServiceOwner input and column.
12. `TemplatesTable.tsx` — system badge, Clone button.
13. `ReleasesTable.tsx` + `ReleaseDetailPage.tsx` — Delete Draft flows.
