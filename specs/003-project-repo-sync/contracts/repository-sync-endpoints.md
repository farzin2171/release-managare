# Contract: Repository Sync Endpoints

**Controller**: `RepositorySyncsController`  
**Base path**: `/api/v1`  
**Auth**: `[Authorize]` on all (Viewer + Admin)

---

## POST `/api/v1/repositories/{id}/sync`

Enqueues a sync for the specified repository. Returns immediately with the created `RepositorySync` row in `Pending` status.

**Path params**: `id` (Guid) — repository ID

**Request body**: none

**Success response** — `202 Accepted`
```json
{
  "id": "...",
  "repositoryId": "...",
  "projectSyncId": null,
  "fromTag": "v2.4.1",
  "toCommitSha": null,
  "status": "Pending",
  "skipReason": null,
  "currentStep": null,
  "startedAt": "2026-05-16T10:00:00Z",
  "completedAt": null,
  "commitCount": 0,
  "ticketCount": 0,
  "contributorCount": 0,
  "breakingChangeCount": 0,
  "contributors": [],
  "errorMessage": null
}
```

**Error responses**:

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | Repository has no `LatestTag` set (cannot sync; returns ProblemDetails with `"NoPinnedTag"` detail) |
| `404 Not Found` | Repository `{id}` does not exist |
| `409 Conflict` | A sync is already `InProgress` for this repository (FR-024); ProblemDetails body |

---

## GET `/api/v1/repositories/{id}/sync/latest`

Returns the latest `RepositorySync` record for the repository's **current pinned tag** (any terminal status). Used by the frontend to restore card state after SSE disconnect or page refresh for standalone syncs.

**Path params**: `id` (Guid) — repository ID

**Success response** — `200 OK` with `RepositorySyncDto` body (same schema as POST above)

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | No sync record exists for this repo + current tag pair, or repository does not exist |

---

## GET `/api/v1/repository-syncs/{syncId}`

Returns a single `RepositorySync` detail record by its own ID. Used by the "View run" drawer when opened from a standalone repo card.

**Path params**: `syncId` (Guid) — sync record ID

**Success response** — `200 OK` with `RepositorySyncDto` body (includes `contributors` array)

**Error responses**:

| Status | Condition |
|--------|-----------|
| `404 Not Found` | Sync record `{syncId}` does not exist |
