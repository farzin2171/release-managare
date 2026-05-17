# Implementation Plan: Project Screen — Repository Sync & Changes Persistence

**Branch**: `003-project-repo-sync` | **Date**: 2026-05-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-project-repo-sync/spec.md`

## Summary

Extend the project detail screen with two new data tables (`RepositorySyncs`, `ProjectSyncs`), a background sync worker, an in-memory SSE event publisher, and eight new API endpoints. The frontend gains a "Project last synced" strip, a five-state per-card sync state machine, a contributors popover, and a "View run" drawer — all additive to the existing layout. Sync is idempotent, persists to SQLite, and enables the project screen to render all metrics on load without any live Git provider call.

## Technical Context

**Language/Version**: Backend — .NET 10, C# 13; Frontend — React 18, TypeScript 5.x, Vite 5.x  
**Primary Dependencies**: ASP.NET Core Web API, EF Core 10 (SQLite), Serilog, FluentValidation, Mapster, Polly; TanStack Query v5, Zustand, shadcn/ui, React Router v6, openapi-typescript  
**Storage**: SQLite (WAL mode); two new tables (`RepositorySyncs`, `ProjectSyncs`); existing `Commits` and `Tickets` tables written by existing services and reused unchanged  
**Testing**: xunit + FluentAssertions + Moq (backend unit/integration); Playwright (E2E)  
**Target Platform**: Linux Docker container (single-container deployment)  
**Project Type**: Web application — ASP.NET Core API + React SPA  
**Performance Goals**: Single-repo sync ≤30s for up to 500 commits; screen load ≤3s from DB; project-wide sync ≤10 min for 20 repos × 200 commits each  
**Constraints**: No new NuGet dependencies; additive-only DB schema; card state colours must use CSS design tokens (not hardcoded hex); commit cap of 5,000 per sync run  
**Scale/Scope**: Projects of up to ~20 repositories; single-tenant deployment

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Layered Architecture | ✅ PASS | Two new Domain aggregates; three new Application service interfaces; implementations + worker in Infrastructure; two new controllers in Api. Dependency direction strictly downward. |
| II. API-First Design | ✅ PASS | All 8 new endpoints under `/api/v1`. OpenAPI spec must be regenerated; frontend client generated via openapi-typescript. SSE endpoint uses built-in `Results.ServerSentEvents` — no new library. |
| III. TDD | ✅ PASS | Domain state-machine tests written red-first. Integration tests use real SQLite per-test temp file. Playwright E2E covers full project sync happy path with SSE stream. |
| IV. Security by Default | ✅ PASS | All new endpoints carry `[Authorize]` (no `Admin` role gate — sync is read-replication per FR-001). No secrets involved in sync data. |
| V. Observability | ✅ PASS | Structured log per repo sync: correlation ID, repo name, commit count, elapsed ms, outcome. Counters: `sync.repository.completed`, `sync.repository.failed`, `sync.project.completed` via existing `/metrics` endpoint. |
| VI. Simplicity | ⚠️ WATCH | `ProjectSyncService.ExecuteAsync` iterates repos sequentially — will approach 30-line limit. MUST be split into private helpers (`ProcessRepoAsync`, `HandleCancellation`). `ISyncJobQueue` and `ISyncEventPublisher` are internal testability seams only, not new extensibility points. |
| VII. Extensibility | ✅ PASS | Reuses `IGitProvider` / `IGitProviderFactory` and `IConventionalCommitParser` unchanged. No new external extensibility seams. New internal interfaces serve decoupling and testability only. |
| VIII. UX Standards | ⚠️ FLAG — MUST FIX | Source guidance references hardcoded hex colours (`#E6F1FB`, `#85B7EB`, `#FCEBEB`, `#F09595`) for card state styling. These MUST become CSS variables (`--color-background-info`, `--color-border-info`, `--color-background-danger`, `--color-border-danger`) before implementation. All new components use shadcn/ui primitives only. |
| IX. Data Integrity | ✅ PASS | Commit + ticket writes and `RepositorySyncs` row update wrapped in a single transaction per sync run. Unique partial index on `ProjectSyncs` enforces one active run per project at the DB level. Idempotency enforced by upsert on `(RepositoryId, Sha)` for commits. |

**Gate result: PASS with two action items** — (1) stay under 30-line method limit in `ProjectSyncService`; (2) replace all hardcoded card-state hex values with CSS variables before the first frontend file is written.

## Project Structure

### Documentation (this feature)

```text
specs/003-project-repo-sync/
├── plan.md                                   ← this file
├── research.md                               ← Phase 0
├── data-model.md                             ← Phase 1
├── quickstart.md                             ← Phase 1
├── contracts/
│   ├── repository-sync-endpoints.md          ← Phase 1
│   └── project-sync-endpoints.md            ← Phase 1
└── tasks.md                                  ← Phase 2 (/speckit-tasks, not this command)
```

### Source Code (new and edited files)

```text
backend/src/RepoManager.Domain/
  Aggregates/RepositorySync.cs                ← new
  Aggregates/ProjectSync.cs                   ← new
  Enums/SyncStatus.cs                         ← new
  Enums/ProjectSyncStatus.cs                  ← new
  Enums/SyncStep.cs                           ← new
  ValueObjects/ContributorSnapshot.cs         ← new

backend/src/RepoManager.Application/
  Services/IRepositorySyncService.cs          ← new
  Services/IProjectSyncService.cs             ← new
  Services/IProjectSyncSnapshotService.cs     ← new
  Queues/ISyncJobQueue.cs                     ← new
  Queues/SyncJob.cs                           ← new
  Events/ISyncEventPublisher.cs               ← new
  Events/SyncEvent.cs                         ← new
  DTOs/RepositorySyncDto.cs                   ← new
  DTOs/ProjectSyncDto.cs                      ← new
  DTOs/RepoSyncSnapshotItemDto.cs             ← new

backend/src/RepoManager.Infrastructure/
  Sync/RepositorySyncService.cs               ← new
  Sync/ProjectSyncService.cs                  ← new
  Sync/ProjectSyncSnapshotService.cs          ← new
  Sync/SyncBackgroundService.cs               ← new
  Sync/InMemorySyncJobQueue.cs                ← new
  Sync/InMemorySyncEventPublisher.cs          ← new
  Persistence/Configurations/
    RepositorySyncConfiguration.cs            ← new
    ProjectSyncConfiguration.cs               ← new
  Persistence/Migrations/[ts]_AddSyncTables.cs ← new (generated via dotnet ef)

backend/src/RepoManager.Api/
  Controllers/RepositorySyncsController.cs    ← new
  Controllers/ProjectSyncsController.cs       ← new

backend/tests/RepoManager.Domain.Tests/
  RepositorySyncStateTests.cs                 ← new (red-first)
  ProjectSyncStateTests.cs                    ← new (red-first)

backend/tests/RepoManager.Infrastructure.Tests/
  SyncBackgroundServiceTests.cs               ← new
  ProjectSyncConcurrencyTests.cs              ← new
  InMemorySyncEventPublisherTests.cs          ← new

backend/tests/RepoManager.Api.Tests/
  RepositorySyncIntegrationTests.cs           ← new
  ProjectSyncIntegrationTests.cs              ← new

frontend/src/lib/api/
  syncApi.ts                                  ← new (generated client wrappers)
  syncSse.ts                                  ← new (EventSource wrapper)

frontend/src/features/projects/
  hooks/useProjectSyncSnapshot.ts             ← new
  hooks/useRepositorySync.ts                  ← new
  hooks/useProjectSync.ts                     ← new
  components/ProjectSyncStrip.tsx             ← new
  components/RepoCardSyncFooter.tsx           ← new
  components/RepoCardSyncOverlay.tsx          ← new
  components/ContributorsPopover.tsx          ← new
  components/ProjectSyncRunDrawer.tsx         ← new
  pages/ProjectDetailPage.tsx                 ← edit: insert <ProjectSyncStrip /> (1 line)
  components/RepositoryCard.tsx               ← edit: overlay wrap + footer + tag chip (3 edits)
```

**Structure Decision**: Web application layout (backend + frontend). All new code isolated to `Sync/` namespace in Infrastructure, `features/projects/` in frontend. Existing files receive exactly two surgical edits.

## Complexity Tracking

No constitution violations requiring justification. Both watch items are implementation-level guardrails, not architectural deviations.
