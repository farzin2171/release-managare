# Implementation Plan: Per-Repo Jira Coverage

**Branch**: `004-repo-jira-coverage` | **Date**: 2026-05-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/004-repo-jira-coverage/spec.md`

## Summary

Surface per-repository Jira ticket coverage at all times — not only during release creation — so admins and viewers can spot drift between commits since the last tag and tickets tagged in Jira for the next release the moment it appears.

The feature adds a `RepoJiraComparisonSnapshot` entity (cached comparison result per repo), a `SemVer` domain value object for next-version arithmetic, a new `IRepoJiraComparisonService` implementation, three extensions to `IJiraService`, two new API endpoints and one action endpoint, a background refresh service, per-repo coverage cards on the project page, and a "Jira coverage" tab on the repository page.

## Technical Context

**Language/Version**: C# 13 / .NET 10; TypeScript 5.x
**Primary Dependencies**: ASP.NET Core Web API, EF Core 10, FluentValidation, Mapster, Serilog, Polly; React 18, TanStack Query, shadcn/ui
**Storage**: SQLite via EF Core 10 (WAL mode) — one new table (`RepoJiraComparisonSnapshots`), one nullable column on `Repositories` (`LastViewedAt`); migration is additive only
**Testing**: xunit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing (backend); Vitest + Playwright (frontend)
**Target Platform**: Existing Docker container — no new infrastructure required
**Project Type**: Feature addition to an existing full-stack web service
**Performance Goals**: Project page loads cached coverage for all repos in < 2 s (SC-001); forced re-sync completes in < 30 s per repo (SC-002)
**Constraints**: External APIs (Git, Jira) MUST NOT be called from request handlers except on cache miss or forced refresh; all API calls go through existing Polly-wrapped HttpClient registrations
**Scale/Scope**: One snapshot per repo, refreshed every 5–10 minutes; expected ≤ 50 repos per project in typical deployments

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Layered Architecture, Not CQRS | ✅ PASS | `SemVer` + `HealthBand` in Domain; `IRepoJiraComparisonService` interface + DTOs in Application; `RepoJiraComparisonService` + `JiraCoverageRefreshService` in Infrastructure; `JiraCoverageController` in Api — strict downward deps, no MediatR |
| II | API-First Design | ✅ PASS | Three new endpoints under `/api/v1`; `RepoJiraComparisonDto` and `ProjectJiraCoverageDto` are the authoritative contract; frontend client regenerated from updated OpenAPI spec via `openapi-typescript` |
| III | Test-Driven Development | ✅ PASS | `SemVer.TryParse` and `NextMinor()` unit-tested first (TDD, 7 cases from spec); `RepoJiraComparisonService` integration-tested against real SQLite + WireMock for Jira/Git; Playwright E2E for the Admin add-ticket and re-sync flows |
| IV | Security by Default | ✅ PASS | GET endpoints require `[Authorize]`; POST add-ticket requires `[Authorize(Roles = "Admin")]`; no Jira token or PAT ever returned to client; Jira project keys read from encrypted `GitProviderConnection`-linked project config |
| V | Observability | ✅ PASS | Six structured log events (see quickstart.md); every computation logs `repoId`, `durationMs`, `matchRate`; correlation ID propagated through all log entries; cache hit/miss distinguished |
| VI | Simplicity Over Cleverness | ✅ PASS | One new service, one new entity; `IHostedService` for background refresh (no Quartz); JSON columns for bucket data consistent with existing `ReleaseReconciliationSnapshot` pattern; no new abstractions beyond what the spec requires |
| VII | Extensibility Where It Matters | ✅ PASS | `IRepoJiraComparisonService` is NOT made extensible (single implementation expected); three new methods on existing `IJiraService` seam; `IGitProvider` used via existing seam — no new interfaces |
| VIII | UX Standards | ✅ PASS | shadcn `Card`, `Tabs`, `Badge`, `Tooltip`, `Collapsible`, `Skeleton` (loading), `Button`; `AlertDialog` for add-ticket confirmation (destructive action); loading state on re-sync button; error message on sync failure; Viewer sees all data, no missing-permission blank states |
| IX | Data Integrity | ✅ PASS | External data (Git commits, Jira tickets) cached in SQLite before display; snapshot invalidated on commit-sync and tag-change writes; background refresh is idempotent; `AddTicketToFixVersionAsync` is idempotent (Jira API no-ops on duplicate) |

**Post-Phase-1 re-check**: All principles confirmed satisfied by data model (additive migration, no cross-aggregate FK beyond `RepositoryId`) and API contract. No violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/004-repo-jira-coverage/
├── plan.md              # This file
├── research.md          # Phase 0 — decisions and rationale
├── data-model.md        # Phase 1 — new entity, value object, DTOs
├── quickstart.md        # Phase 1 — developer implementation guide
├── contracts/
│   ├── api-endpoints.md          # Phase 1 — endpoint contracts
│   └── service-interfaces.md     # Phase 1 — new and extended interfaces
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (affected paths)

This feature modifies existing source paths and creates new files. No new top-level directories are introduced.

```text
backend/
├── src/
│   ├── RepoManager.Domain/
│   │   ├── Entities/
│   │   │   └── Repository.cs                    # + LastViewedAt + JiraComparisonSnapshots nav prop
│   │   ├── Enums/
│   │   │   └── HealthBand.cs                    # NEW — Green / Amber / Red / Unknown
│   │   └── ValueObjects/
│   │       └── SemVer.cs                        # NEW — TryParse, NextMinor, ToString
│   ├── RepoManager.Application/
│   │   └── Jira/
│   │       ├── IRepoJiraComparisonService.cs    # NEW — GetForRepoAsync, GetForProjectAsync, AddTicketToFixVersionAsync
│   │       ├── IJiraService.cs                  # MODIFIED — + GetTicketsInFixVersionAsync, AddTicketToFixVersionAsync, CreateFixVersionAsync
│   │       └── Dtos/
│   │           ├── RepoJiraComparisonDto.cs     # NEW
│   │           ├── ProjectJiraCoverageDto.cs    # NEW
│   │           ├── ComparisonCounts.cs          # NEW
│   │           ├── TicketSummaryDto.cs          # NEW
│   │           ├── CommitSummaryDto.cs          # NEW
│   │           ├── AddToFixVersionResultDto.cs  # NEW
│   │           └── JiraIssueSummary.cs          # NEW
│   ├── RepoManager.Infrastructure/
│   │   ├── Jira/
│   │   │   ├── RepoJiraComparisonService.cs     # NEW — IRepoJiraComparisonService impl
│   │   │   └── JiraService.cs                   # MODIFIED — + 3 new method implementations
│   │   ├── BackgroundServices/
│   │   │   └── JiraCoverageRefreshService.cs    # NEW — IHostedService, 10 min poll
│   │   └── Persistence/
│   │       ├── Configurations/
│   │       │   └── RepoJiraComparisonSnapshotConfiguration.cs  # NEW — EF config
│   │       └── Migrations/
│   │           └── AddRepoJiraComparisonSnapshot/              # NEW migration
│   └── RepoManager.Api/
│       └── Controllers/
│           └── JiraCoverageController.cs        # NEW — 3 endpoints
├── tests/
│   ├── RepoManager.UnitTests/
│   │   └── Domain/
│   │       └── SemVerTests.cs                  # NEW — TDD, 7 test cases
│   └── RepoManager.IntegrationTests/
│       ├── Api/
│       │   └── JiraCoverageTests.cs             # NEW — cache hit/miss, RBAC, 404/409/422
│       └── Infrastructure/
│           └── RepoJiraComparisonServiceTests.cs # NEW — WireMock for Jira + real SQLite

frontend/
└── src/
    ├── features/
    │   └── jira-coverage/
    │       ├── components/
    │       │   ├── HealthPill.tsx               # NEW — coloured Badge by HealthBand
    │       │   ├── BucketList.tsx               # NEW — collapsible three-bucket renderer
    │       │   ├── RepoCoverageCard.tsx          # NEW — project page card
    │       │   ├── ProjectCoverageAggregate.tsx  # NEW — aggregate header strip
    │       │   └── RepoCoverageTab.tsx           # NEW — full service-page tab
    │       └── hooks/
    │           └── useJiraCoverage.ts            # NEW — TanStack Query wrappers
    ├── features/
    │   ├── projects/
    │   │   └── pages/
    │   │       └── ProjectDetailPage.tsx         # MODIFIED — add coverage cards + aggregate header
    │   └── repositories/
    │       └── pages/
    │           └── RepositoryDetailPage.tsx      # MODIFIED — add "Jira coverage" tab
    └── lib/
        └── api.d.ts                              # REGENERATED after backend changes
```

**Structure Decision**: Additive changes only to the existing full-stack web application layout. All new frontend code lives under `features/jira-coverage/`. No new top-level directories, no new .NET projects.

## Complexity Tracking

> No constitution violations found. This section is intentionally empty.
