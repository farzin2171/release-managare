# Implementation Plan: Latest Tag Selection for Repositories

**Branch**: `002-latest-tag-selection` | **Date**: 2026-05-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-latest-tag-selection/spec.md`

## Summary

Extend the existing `Repository` entity with four new fields that capture a pinned "latest tag" — the tag name, its commit SHA at pin time, the timestamp, and the identity of the Admin who pinned it. Expose live tag listing, set, and clear operations via three new API endpoints. Surface the pinned tag on the project detail screen's repositories table.

This is an additive vertical slice: a new EF Core migration (`AddLatestTagToRepositories`), an extension to `IGitProviderService` and `IRepositoryService`, three new controller actions on `RepositoriesController`, and frontend additions to the Settings → Repositories sheet and the Project detail screen.

## Technical Context

**Language/Version**: C# 13 / .NET 10; TypeScript 5.x
**Primary Dependencies**: ASP.NET Core Web API, EF Core 10, FluentValidation, Mapster, Serilog, Polly; React 18, TanStack Query, shadcn/ui
**Storage**: SQLite via EF Core 10 (WAL mode) — additive migration only; no data backfill
**Testing**: xunit, FluentAssertions, Moq, Microsoft.AspNetCore.Mvc.Testing (backend); Vitest + Playwright (frontend)
**Target Platform**: Existing Docker container (Linux) — no new infrastructure required
**Project Type**: Feature addition to an existing full-stack web service
**Performance Goals**: Tag list loads in under 5 s (SC-002); project screen reflects saved tag on next load (SC-003)
**Constraints**: No caching of tag list between sessions; last-write-wins on concurrent pins; `IsTracked` guard enforced in domain layer
**Scale/Scope**: Up to 200 tags per repository, single Admin performing pin actions, two user roles

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Layered Architecture, Not CQRS | ✅ PASS | New domain methods on `Repository` entity; new service methods on `IRepositoryService`; new controller actions on `RepositoriesController` — no MediatR, strict downward deps |
| II | API-First Design | ✅ PASS | Three new endpoints under `/api/v1`; `RepositoryDto` extended; frontend client regenerated from updated OpenAPI spec |
| III | Test-Driven Development | ✅ PASS | Domain unit tests for `PinLatestTag` / `ClearLatestTag` written first (TDD); integration tests hit real SQLite; WireMock test for `ListTagsAsync`; Playwright E2E for Admin pin flow |
| IV | Security by Default | ✅ PASS | GET `/repositories/{id}/tags` requires `[Authorize]`; PUT and DELETE require `[Authorize(Roles = "Admin")]`; PAT read from encrypted `GitProviderConnection` — never exposed to client |
| V | Observability | ✅ PASS | Each tag fetch logs `repository.tags.fetched` (duration, count, outcome); each set/clear logs `repository.latest_tag.changed` (old → new); all entries carry correlation ID |
| VI | Simplicity Over Cleverness | ✅ PASS | No new abstractions; `ListTagsAsync` added to existing `IGitProvider` seam; guard logic lives in domain entity; no feature flag needed |
| VII | Extensibility Where It Matters | ✅ PASS | `ListTagsAsync` added to `IGitProvider` — GitHub/GitLab implementations can add it with no changes to callers |
| VIII | UX Standards | ✅ PASS | shadcn `Sheet` for detail panel, `DataTable` for tag list, `Badge` + `Tooltip` for project screen, `AlertDialog` for clear confirmation; loading + error states on tag fetch; amber dot for unset repos |
| IX | Data Integrity | ✅ PASS | No cross-aggregate write (single Repository entity updated); `LatestTagSetAt` is server-generated UTC; audit entry written in the same service call as the save |

**Post-Phase-1 re-check**: All principles confirmed satisfied by data model (additive migration, nullable FK) and API contract design. No violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/002-latest-tag-selection/
├── plan.md              # This file
├── research.md          # Phase 0 — key decisions and rationale
├── data-model.md        # Phase 1 — schema changes and domain methods
├── quickstart.md        # Phase 1 — developer notes for this feature
├── contracts/           # Phase 1 — API endpoints and service interface extensions
└── tasks.md             # Phase 2 output (/speckit-tasks command — NOT created here)
```

### Source Code (affected paths)

This feature modifies existing source paths. No new top-level directories are introduced.

```text
backend/
├── src/
│   ├── RepoManager.Domain/
│   │   ├── Entities/
│   │   │   └── Repository.cs                  # + PinLatestTag / ClearLatestTag + 4 new properties
│   │   └── ValueObjects/
│   │       └── RepositoryTag.cs               # NEW — { Name, CommitSha, CommitDate, AuthorName }
│   ├── RepoManager.Application/
│   │   ├── Repositories/
│   │   │   ├── IRepositoryService.cs          # + GetTagsAsync, SetLatestTagAsync, ClearLatestTagAsync
│   │   │   └── Dtos/
│   │   │       └── RepositoryDto.cs           # + LatestTag, LatestTagCommitSha, LatestTagSetAt, LatestTagSetBy
│   │   └── GitProviders/
│   │       └── IGitProviderService.cs         # + ListTagsAsync
│   ├── RepoManager.Infrastructure/
│   │   ├── Repositories/
│   │   │   └── RepositoryService.cs           # + GetTagsAsync, SetLatestTagAsync, ClearLatestTagAsync
│   │   ├── GitProviders/
│   │   │   └── AzureDevOpsGitProvider.cs      # + ListTagsAsync (refs API + commit batch)
│   │   └── Persistence/
│   │       ├── Configurations/
│   │       │   └── RepositoryConfiguration.cs # + column mappings + FK for LatestTagSetBy
│   │       └── Migrations/
│   │           └── AddLatestTagToRepositories/ # NEW migration
│   └── RepoManager.Api/
│       └── Controllers/
│           └── RepositoriesController.cs      # + GetTags, SetLatestTag, ClearLatestTag actions
├── tests/
│   ├── RepoManager.UnitTests/
│   │   └── Domain/
│   │       └── RepositoryLatestTagTests.cs    # NEW — TDD unit tests for domain methods
│   └── RepoManager.IntegrationTests/
│       ├── Api/
│       │   └── RepositoryLatestTagTests.cs    # NEW — Admin/Viewer RBAC, 404/422 cases
│       └── Infrastructure/
│           └── AzureDevOpsListTagsTests.cs    # NEW — WireMock server tests

frontend/
└── src/
    ├── features/
    │   ├── repositories/
    │   │   ├── components/
    │   │   │   ├── RepositoryDetailSheet.tsx  # NEW — Sheet with pinned tag + Fetch/Set/Clear
    │   │   │   └── TagPickerDialog.tsx        # NEW — DataTable of tags with search + confirm
    │   │   └── api/
    │   │       └── repositoriesApi.ts         # + getRepositoryTags, setLatestTag, clearLatestTag
    │   └── projects/
    │       └── components/
    │           └── ProjectRepositoriesTable.tsx # MODIFIED — + Latest tag column, Badge, Tooltip, amber dot
    └── lib/
        └── api.d.ts                           # REGENERATED after backend endpoint changes
```

**Structure Decision**: Additive changes only to the existing web application layout. Backend and frontend each gain new files within their existing feature folders. No new top-level directories, no new .NET projects.

## Complexity Tracking

> No constitution violations found. This section is intentionally empty.
