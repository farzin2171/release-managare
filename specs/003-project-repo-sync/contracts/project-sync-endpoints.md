# Contract: Project Sync Endpoints

**Controller**: `ProjectSyncsController`  
**Base path**: `/api/v1`  
**Auth**: `[Authorize]` on all (Viewer + Admin)

---

## POST `/api/v1/projects/{id}/sync`

Starts a project-wide sync. Enqueues the run and returns immediately with the created `ProjectSync` row in `Pending` status.

**Path params**: `id` (Guid) — project ID

**Request body**: none

**Success response** — `202 Accepted`
```json
{
  "id": "...",
  "projectId": "...",
  "status": "Pending",
  "startedAt": "2026-05-16T10:00:00Z",
  "completedAt": null,
  "totalRepos": 18,
  "succeededCount": 0,
  "failedCount": 0,
  "skippedCount": 0,
  "triggeredByUserId": "..."
}
```

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | Project `{id}` does not exist |
| `409 Conflict` | A project sync is already `Pending` or `InProgress` for this project; ProblemDetails body with `"SyncAlreadyRunning"` detail |

---

## DELETE `/api/v1/projects/{id}/sync/active`

Cancels the active project sync. Sets the `ProjectSync` status to `Cancelling` (internal transient); the worker observes this flag after the in-flight repo completes and marks the run `Cancelled`.

**Path params**: `id` (Guid) — project ID

**Success response** — `200 OK`
```json
{ "message": "Cancellation requested. The in-progress repository will complete before the run stops." }
```

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | Project `{id}` does not exist, or no active sync exists for this project |

---

## GET `/api/v1/projects/{id}/sync/latest`

Returns the most recent `ProjectSync` record for the project, including its child `RepositorySync` rows. Used by the "View run" drawer.

**Path params**: `id` (Guid) — project ID

**Success response** — `200 OK`
```json
{
  "id": "...",
  "projectId": "...",
  "status": "PartiallyFailed",
  "startedAt": "2026-05-16T09:00:00Z",
  "completedAt": "2026-05-16T09:12:00Z",
  "totalRepos": 18,
  "succeededCount": 16,
  "failedCount": 1,
  "skippedCount": 1,
  "triggeredByUserId": "...",
  "childSyncs": [ /* array of RepositorySyncDto */ ]
}
```

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | No project sync record exists for this project, or project does not exist |

---

## GET `/api/v1/projects/{id}/sync/active`

Returns the currently `Pending` or `InProgress` project sync, or `204 No Content` if none exists. Used by the frontend as an SSE fallback-polling target.

**Path params**: `id` (Guid) — project ID

**Success response** — `200 OK` with `ProjectSyncDto` body (without `childSyncs`), or `204 No Content`

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | Project `{id}` does not exist |

---

## GET `/api/v1/projects/{id}/sync/active/stream`

SSE stream for live progress updates on the active project sync. The client subscribes on sync start and keeps the connection open until a `complete` event is received or the connection is dropped.

**Path params**: `id` (Guid) — project ID

**Response**: `Content-Type: text/event-stream`

**Event types**:

| `event` field | When emitted | `data` payload |
|--------------|-------------|----------------|
| `repo_started` | A repo's sync flips to `InProgress` | `{ repoId, repoName, syncId, totalRepos, completedCount }` |
| `step_changed` | `CurrentStep` changes on the active `RepositorySync` | `{ repoId, syncId, currentStep, elapsedMs }` |
| `repo_completed` | A repo's sync reaches `Succeeded`, `Failed`, or `Skipped` | `{ repoId, syncId, status, commitCount, ticketCount, breakingChangeCount, contributorCount, errorMessage? }` |
| `project_complete` | All repos processed; run reaches terminal status | `{ projectSyncId, status, succeededCount, failedCount, skippedCount, completedAt }` |

**Reconnect behaviour**: The client sends `Last-Event-ID` on reconnect; the server re-emits any events since that ID from an in-memory buffer (last 50 events). If the run has already completed, the server immediately emits a `project_complete` event and closes the stream.

**Error responses**:

| Status | Condition |
|--------|-----------|
| `204 No Content` | No active sync exists for this project (stream not opened; client should fall back to polling) |
| `404 Not Found` | Project `{id}` does not exist |

---

## GET `/api/v1/projects/{id}/repositories/sync-snapshot`

Returns a snapshot of the latest successful sync state for every repository assigned to the project. This is the **single endpoint called on project screen load** to populate all card metrics without any Git provider call.

**Path params**: `id` (Guid) — project ID

**Query params**: none

**Caching**: Response cached for 5 seconds per `projectId` at the application layer (invalidated immediately when any child sync completes).

**Success response** — `200 OK`
```json
[
  {
    "repositoryId": "...",
    "repositoryName": "backend-api",
    "latestTag": "v2.4.1",
    "latestSync": {
      "id": "...",
      "status": "Succeeded",
      "fromTag": "v2.4.1",
      "startedAt": "2026-05-16T08:00:00Z",
      "completedAt": "2026-05-16T08:01:12Z",
      "commitCount": 34,
      "ticketCount": 12,
      "breakingChangeCount": 1,
      "contributorCount": 5,
      "contributors": [
        { "name": "Alice Martin", "email": "alice@example.com", "commits": 18 }
      ]
    },
    "currentStep": null
  },
  {
    "repositoryId": "...",
    "repositoryName": "frontend-app",
    "latestTag": null,
    "latestSync": null,
    "currentStep": null
  }
]
```

`latestSync` is `null` when the repository has never been synced against its current `latestTag` (or has no pinned tag). `currentStep` is non-null only when a sync is currently `InProgress` for this repo — the frontend uses this to show the live status message on screen load if a sync was already running.

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | Project `{id}` does not exist |
