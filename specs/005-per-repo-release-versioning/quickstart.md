# Quickstart: Per-Repo Release Versioning

**Branch**: `006-per-repo-release-versioning`  
**Date**: 2026-05-23

## What This Feature Adds

This feature changes how a release is composed. Instead of a single project-wide version entered by the user, a release now:

- Includes a **selected subset of the project's repos**.
- Stores a **per-repo version snapshot** (previous version → next version + change counts) at the moment of creation.
- Derives the **release-level version label** automatically from the primary repo's confirmed next version.

## Prerequisites

The following milestones must be complete before implementing this feature (per `docs/04-tasks-guidance.md`):

1. Foundation (auth, health endpoints) ✅
2. Git provider integration ✅
3. Repository sync ✅
4. Logical projects ✅
5. Conventional commit parsing ✅
6. Project change visibility ✅
7. Release creation (single-version) ✅ — this feature extends milestone 7

## Implementation Sequence

### Step 1 — Backend: Domain entity

Add `ReleaseRepository` class to `RepoManager.Domain`, add navigation property to `Release`.

### Step 2 — Backend: EF Core migration

Run `dotnet ef migrations add AddReleaseRepository` and fill in the backfill SQL as documented in `data-model.md`.

### Step 3 — Backend: IVersionBumpService

Move existing bump logic behind `IVersionBumpService` (Application layer). Implement in Infrastructure. Register in DI. Write unit tests first (TDD required per constitution).

### Step 4 — Backend: IReleaseCompositionService

Implement `ReleaseCompositionService` in Infrastructure. Use `IVersionBumpService` for per-repo snapshots. Register in DI. Write unit tests for `DeriveReleaseVersion` before implementation.

### Step 5 — Backend: Controller & validation

Add `ReleasesController` (or extend existing) with the 6 endpoints defined in `contracts/api-endpoints.md`. Wire `CreateReleaseRequestValidator` to FluentValidation DI.

### Step 6 — Frontend: Generate API client

Run `openapi-typescript` to regenerate the client after the backend is running.

### Step 7 — Frontend: ProjectReleasesList

Implement `<ProjectReleasesList />` and add as a new tab on the project detail page. Data via TanStack Query.

### Step 8 — Frontend: ReleaseRepoSelectionStep

Implement `<ReleaseRepoSelectionStep />` as a new wizard step (between "Confirm change range" and "Choose template"). Calls `POST /preview` on mount.

### Step 9 — Frontend: ReleaseDetailPage update

Add `<ReleaseRepositoriesTable />` section to the existing release detail page.

### Step 10 — Integration tests

Full create-release flow: preview → create → fetch → assert snapshot fields. Backfill migration test.

## Key Commands

```powershell
# Run migration after entity changes
dotnet ef migrations add AddReleaseRepository \
  --project backend/src/RepoManager.Infrastructure \
  --startup-project backend/src/RepoManager.Api

# Apply migration
dotnet ef database update \
  --project backend/src/RepoManager.Infrastructure \
  --startup-project backend/src/RepoManager.Api

# Run unit tests for this feature
dotnet test --filter "FullyQualifiedName~ReleaseComposition|VersionBump"

# Regenerate frontend API client (from frontend/)
npm run generate-client
```

## Smoke Test

1. Create a project with 3 repos, each with at least one commit since their last tag.
2. Open the release creation wizard.
3. Confirm the repo selection step shows all 3 repos, pre-selects those with changes, and shows suggested versions.
4. Deselect one repo. Verify the release label updates to reflect the primary repo's version.
5. Submit. Verify the release detail page shows a per-repo table with correct previous → next versions.
6. Attempt to edit the published release's repo selection. Verify the edit is blocked.
