# API Endpoints Contract: Latest Tag Selection

**Base path**: `/api/v1`  
**Auth**: JWT Bearer (all endpoints)  
**Error format**: RFC 7807 ProblemDetails  
**Scope**: New and modified endpoints for the latest-tag feature only.

---

## Repositories — Latest Tag

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/repositories/{id}/tags` | Admin | Fetch live tag list from provider |
| PUT | `/repositories/{id}/latest-tag` | Admin | Pin a tag as the repository's latest |
| DELETE | `/repositories/{id}/latest-tag` | Admin | Clear the pinned latest tag |
| GET | `/repositories/{id}` | Any | *(Modified)* — response includes new `latestTag` fields |

---

### GET /repositories/{id}/tags

**Auth**: `[Authorize]` (Admin required per feature spec)  
**Description**: Returns the live tag list fetched synchronously from the Git provider. The client must display a loading state while the request is in flight; tags are not cached in SQLite.

```json
// Response 200
{
  "tags": [
    {
      "name": "v1.2.3",
      "commitSha": "abc1234def5678...",
      "commitDate": "2026-04-10T14:22:00Z",
      "authorName": "Jane Smith"
    }
  ]
}
```

| Status | Condition |
|--------|-----------|
| 200 | Tags returned successfully |
| 404 | Repository not found |
| 422 | Repository exists but `isTracked` is `false` |

> **Loading note**: This endpoint makes a synchronous call to the Git provider (Azure DevOps `/_apis/git/repositories/{externalId}/refs`). Clients must show a loading/spinner state and not assume an instant response.

---

### PUT /repositories/{id}/latest-tag

**Auth**: `[Authorize(Roles = "Admin")]`  
**Description**: Pins the named tag as the repository's latest. The server re-fetches tags from the provider to validate the tag exists and to capture the current commit SHA at the time of pinning. Writes an audit entry.

```json
// Request body
{ "tagName": "v1.2.3" }

// Response 200 — full updated RepositoryDto (existing fields omitted for brevity)
{
  "id": "guid",
  "name": "my-repo",
  "latestTag": "v1.2.3",
  "latestTagCommitSha": "abc1234def5678...",
  "latestTagSetAt": "2026-05-15T10:00:00Z",
  "latestTagSetBy": { "id": "guid", "email": "admin@example.com" }
}
```

| Status | Condition |
|--------|-----------|
| 200 | Tag pinned; updated `RepositoryDto` returned |
| 404 | Repository not found |
| 422 | Repository is not tracked (`isTracked = false`) |
| 422 | Tag name does not exist in the remote provider |

---

### DELETE /repositories/{id}/latest-tag

**Auth**: `[Authorize(Roles = "Admin")]`  
**Description**: Clears the pinned latest tag, setting `latestTag`, `latestTagCommitSha`, `latestTagSetAt`, and `latestTagSetBy` to `null`. Writes an audit entry.

```
// Response 204 No Content
```

| Status | Condition |
|--------|-----------|
| 204 | Tag cleared (also succeeds if no tag was pinned — idempotent) |
| 404 | Repository not found |

---

### GET /repositories/{id}  *(modified)*

No change to existing fields or status codes. The following fields are added to the response body:

```json
// New fields in response:
{
  "latestTag": "v1.2.3",
  "latestTagCommitSha": "abc1234...",
  "latestTagSetAt": "2026-05-15T10:00:00Z",
  "latestTagSetBy": { "id": "guid", "email": "admin@example.com" }
}
```

All four fields are `null` when no tag has been pinned. `latestTagSetBy` is `null` if the setting user has since been deactivated and the record cannot be resolved.

---

## Common HTTP Status Codes (feature-relevant)

| Code | Meaning |
|------|---------|
| 200 | Success |
| 204 | No Content (DELETE) |
| 401 | Missing or invalid JWT |
| 403 | Insufficient role (Viewer attempting Admin action) |
| 404 | Resource not found |
| 422 | Unprocessable — repository not tracked, or tag does not exist in remote |

All error responses follow RFC 7807:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "Tag 'v9.9.9' does not exist in the remote repository.",
  "traceId": "correlation-id"
}
```
