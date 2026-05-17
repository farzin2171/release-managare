# Quickstart: Project Screen — Repository Sync & Changes Persistence

**Branch**: `003-project-repo-sync` | **Date**: 2026-05-16

This guide covers the minimum steps to get a working development environment for this feature, including the new sync tables and the SSE stream.

## Prerequisites

- .NET 10 SDK
- Node.js 20+ and npm
- An Azure DevOps organisation + personal access token (PAT) with `Code (Read)` scope
- An existing Git provider connection and at least one project with one or more repositories pinned to a latest tag (from Feature 002)

## Backend Setup

### 1 — Create the new migration

```powershell
dotnet ef migrations add AddSyncTables `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

The migration creates:
- `RepositorySyncs` table with all columns and indexes
- `ProjectSyncs` table with the unique partial index on `(ProjectId) WHERE Status IN (0,1)`

### 2 — Apply the migration

```powershell
dotnet ef database update `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

EF Core applies the migration automatically on startup in development — manual `database update` is only needed when running migrations ahead of the server start.

### 3 — Build and run the backend

```powershell
dotnet build backend/src
dotnet run --project backend/src/RepoManager.Api
```

The background sync worker starts alongside the API. On startup it performs the stale-sync recovery pass (marks any `InProgress` rows older than 30 minutes as `Failed`).

### 4 — Verify new endpoints

```powershell
# Check the snapshot endpoint returns an empty array for a project with no syncs
curl -H "Authorization: Bearer <token>" `
  http://localhost:5000/api/v1/projects/<projectId>/repositories/sync-snapshot
```

Expected: `200 OK` with an array where every item has `"latestSync": null`.

## Frontend Setup

### 1 — Install dependencies and regenerate the API client

```powershell
cd frontend
npm install
npm run generate-client   # regenerates lib/api/client.ts from the OpenAPI spec
```

If `generate-client` is not yet in `package.json`, add:
```json
"generate-client": "openapi-typescript http://localhost:5000/openapi/v1.json -o src/lib/api/client.ts"
```

### 2 — Run the dev server

```powershell
npm run dev
```

Navigate to a project detail page. You should see:
- The "Project last synced" strip showing "Never synced" with a "Sync project" button
- Each repository card with a footer showing "Not synced yet" and an enabled (or disabled) "Sync" button depending on whether it has a pinned tag

### 3 — Trigger a test sync

1. Open a project that has at least one repository with a pinned tag
2. Click "Sync" on that repository card
3. The card should enter the "in progress" state (blue tint, live step message)
4. After the sync completes, the four metrics update and the footer shows "just now"

## SSE Stream Manual Test

```bash
# Subscribe to the project sync stream in a terminal (replace IDs and token)
curl -N -H "Authorization: Bearer <token>" \
  -H "Accept: text/event-stream" \
  "http://localhost:5000/api/v1/projects/<projectId>/sync/active/stream"

# In another terminal, start a project sync
curl -X POST -H "Authorization: Bearer <token>" \
  "http://localhost:5000/api/v1/projects/<projectId>/sync"
```

You should see `repo_started`, `step_changed`, `repo_completed`, and finally `project_complete` events appear in the first terminal as the sync progresses.

## Running Tests

```powershell
# Domain unit tests (red-first — write these before implementation)
dotnet test backend/tests/RepoManager.Domain.Tests

# Infrastructure integration tests (requires a temp SQLite file — no mocks)
dotnet test backend/tests/RepoManager.Infrastructure.Tests

# API integration tests
dotnet test backend/tests/RepoManager.Api.Tests

# All backend tests
dotnet test backend/tests

# Frontend unit tests
cd frontend && npm test

# Playwright E2E (backend must be running)
npx playwright test --grep "project sync"
```

## Key Watch Items During Development

1. **CSS variables, not hex**: Card state colours must use `var(--color-background-info)`, `var(--color-border-info)`, `var(--color-background-danger)`, `var(--color-border-danger)` — no hardcoded hex values.
2. **Method length**: `ProjectSyncService.ExecuteAsync` will grow — extract `ProcessRepoAsync` and `HandleCompletionAsync` before the method exceeds 30 lines.
3. **Transaction boundary**: The `RepositorySyncService` private execute method must open a transaction that spans the `Commits` upserts, `Tickets` upserts, and final `RepositorySync` row update — all in one commit.
4. **Snapshot cache invalidation**: After a repo sync completes, invalidate the in-process cache for the parent project's snapshot so the next screen load returns fresh data.
5. **OpenAPI regeneration**: After adding `RepositorySyncsController` or `ProjectSyncsController`, run `npm run generate-client` before writing any frontend hook.
