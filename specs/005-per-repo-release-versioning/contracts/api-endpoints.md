# API Contracts: Per-Repo Release Versioning

**Phase**: 1 — Contracts  
**Branch**: `006-per-repo-release-versioning`  
**Date**: 2026-05-23

All routes are project-scoped. JWT bearer required on all endpoints.  
Write operations require `Admin` role; read operations allow `Viewer` role.

---

## Endpoint Summary

| Method   | Route                                              | Purpose                                      | Role   |
|----------|----------------------------------------------------|----------------------------------------------|--------|
| `GET`    | `/api/v1/projects/{projectId}/releases`            | List releases for a project                  | Viewer |
| `POST`   | `/api/v1/projects/{projectId}/releases/preview`    | Compute per-repo suggestions (read-only)     | Admin  |
| `POST`   | `/api/v1/projects/{projectId}/releases`            | Create a Draft release                       | Admin  |
| `GET`    | `/api/v1/projects/{projectId}/releases/{id}`       | Get a single release with per-repo detail    | Viewer |
| `PUT`    | `/api/v1/projects/{projectId}/releases/{id}`       | Update a Draft release                       | Admin  |
| `DELETE` | `/api/v1/projects/{projectId}/releases/{id}`       | Delete a Draft release                       | Admin  |

---

## GET `/api/v1/projects/{projectId}/releases`

List all releases for a project. Default sort: `createdAt DESC`.

**Query parameters**:
- `?status=Draft|Published|Archived` — filter by status (optional)
- `?search=<name>` — case-insensitive substring match on `Name` (optional)
- `?sort=createdAt|name` — sort field (default: `createdAt`)
- `?order=asc|desc` — sort order (default: `desc`)

**Response 200**:
```json
[
  {
    "id": 42,
    "name": "May Release",
    "version": "2.6.0",
    "status": "Published",
    "createdAt": "2026-05-20T14:02:00Z",
    "publishedAt": "2026-05-20T15:10:00Z",
    "repoCount": 3
  }
]
```

**Errors**:
- `404` — project not found.

---

## POST `/api/v1/projects/{projectId}/releases/preview`

Compute per-repo version suggestions for the wizard. **Read-only — does not persist anything.**

**Request body**:
```json
{ "repositoryIds": [11, 12, 15] }
```

**Validation**:
- All `repositoryIds` must belong to the project.

**Response 200**:
```json
{
  "repositories": [
    {
      "repositoryId": 11,
      "name": "Services.UX",
      "isPrimary": true,
      "hasChanges": true,
      "previousVersion": "1.30.0",
      "suggestedNextVersion": "1.31.0",
      "bumpType": "minor",
      "commitCount": 12,
      "ticketCount": 5
    },
    {
      "repositoryId": 12,
      "name": "Apply.Api",
      "isPrimary": false,
      "hasChanges": false,
      "previousVersion": "2.5.7",
      "suggestedNextVersion": "2.5.8",
      "bumpType": "patch",
      "commitCount": 0,
      "ticketCount": 0
    }
  ],
  "derivedReleaseVersion": "1.31.0",
  "derivedFromRepositoryId": 11
}
```

**Errors**:
- `400 ValidationProblem` — one or more `repositoryIds` not in project.
- `404` — project not found.

---

## POST `/api/v1/projects/{projectId}/releases`

Create a Draft release.

**Request body**:
```json
{
  "name": "May Release",
  "repositories": [
    {
      "repositoryId": 11,
      "nextVersion": "1.31.0",
      "bumpType": "minor"
    },
    {
      "repositoryId": 15,
      "nextVersion": "3.2.0",
      "bumpType": "major"
    }
  ]
}
```

**Server-side validation** (in order):
1. Every `repositoryId` belongs to the project via `ProjectRepository`.
2. At least one repository provided.
3. Each `nextVersion` is a valid semver string.
4. Each `nextVersion` is strictly greater than the freshly-fetched `previousVersion` for that repo.
5. Each `bumpType` is one of `major`, `minor`, `patch`, `manual`.

**Server behaviour**:
- Discards any client-supplied `previousVersion`, `fromCommitSha`, `toCommitSha`, `commitCount`, `ticketCount` — re-derives these via `IVersionBumpService.SuggestAsync`.
- Derives `Release.Version` from the primary repo's `nextVersion`; falls back to alphabetically-first repo if primary is not included.

**Response 201 Created** — body is the full `ReleaseDto` including `releaseRepositories` collection (see GET single release shape below).

**Errors**:
- `400 ValidationProblem` — any validation rule violated; includes `errors` map with field paths and error codes.
- `404` — project not found.

---

## GET `/api/v1/projects/{projectId}/releases/{id}`

Get a single release with full per-repo snapshot detail.

**Response 200**:
```json
{
  "id": 42,
  "projectId": 7,
  "name": "May Release",
  "version": "2.6.0",
  "status": "Published",
  "createdAt": "2026-05-20T14:02:00Z",
  "publishedAt": "2026-05-20T15:10:00Z",
  "confluencePageUrl": "https://example.atlassian.net/wiki/...",
  "notesMarkdown": "## What's new\n...",
  "releaseRepositories": [
    {
      "id": 101,
      "repositoryId": 11,
      "repositoryName": "Services.UX",
      "previousVersion": "1.30.0",
      "nextVersion": "1.31.0",
      "bumpType": "minor",
      "fromCommitSha": "abc1234",
      "toCommitSha": "def5678",
      "commitCount": 12,
      "ticketCount": 5,
      "isLegacy": false
    }
  ]
}
```

`isLegacy` is `true` when the row was backfilled from a pre-feature release (empty snapshot fields).

**Errors**:
- `404` — release or project not found.

---

## PUT `/api/v1/projects/{projectId}/releases/{id}`

Update a Draft release. Replaces the `ReleaseRepositories` collection wholesale and re-derives `Release.Version`. Snapshot fields are re-captured server-side.

**Request body**: same shape as `POST` (minus `name` — name is not editable after creation; ignore or return `400` if sent).

**Errors**:
- `409 Conflict` with `{ "code": "release_not_draft" }` — release is Published or Archived.
- `400 ValidationProblem` — same validation rules as POST.
- `404` — release or project not found.

**Response 200** — updated `ReleaseDto` (same shape as GET single release).

---

## DELETE `/api/v1/projects/{projectId}/releases/{id}`

Delete a Draft release and all its `ReleaseRepository` rows (cascade).

**Errors**:
- `409 Conflict` with `{ "code": "release_not_draft" }` — cannot delete Published or Archived release.
- `404` — release or project not found.

**Response 204 No Content**.
