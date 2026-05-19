# API Endpoints Contract: Per-Repo Jira Coverage

**Base path**: `/api/v1`
**Auth**: JWT Bearer (all endpoints)
**Error format**: RFC 7807 ProblemDetails
**Scope**: New endpoints for the repo-jira-coverage feature only.

---

## Endpoint Summary

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/repositories/{id}/jira-coverage` | Any | Get cached Jira coverage for one repository |
| GET | `/projects/{id}/jira-coverage` | Any | Get Jira coverage for all repos in a project |
| POST | `/repositories/{id}/jira-coverage/add-ticket` | Admin | Add a Git-only ticket to the Jira fix version |

---

### GET /repositories/{id}/jira-coverage

**Auth**: `[Authorize]` (Admin and Viewer)
**Query params**: `?refresh=true` (optional, default `false`) — forces re-sync bypassing the 5-minute TTL cache.
**Description**: Returns the latest cached Jira coverage comparison for one repository. If `refresh=false` and the cache is fresh (< 5 minutes old), returns the cached snapshot immediately. If `refresh=true` or the cache is stale, recomputes synchronously and updates the snapshot before responding.

Updates `Repository.LastViewedAt` to now on every call (regardless of refresh flag) so the background job includes this repo in its 24-hour refresh window.

```json
// Response 200
{
  "repositoryId": 7,
  "repositoryName": "Services.UX",
  "currentTag": "1.30.0",
  "nextVersion": "1.31.0",
  "jiraFixVersionName": "Services.UX_1.31.0",
  "jiraFixVersionExists": true,
  "supported": true,
  "unsupportedReason": null,
  "counts": {
    "commitCount": 12,
    "gitTicketCount": 8,
    "jiraTicketCount": 10,
    "inBothCount": 7,
    "jiraOnlyCount": 3,
    "gitOnlyCount": 1
  },
  "matchRate": 0.875,
  "health": "Amber",
  "inBoth": [
    { "key": "PROJ-100", "summary": "Fix login timeout", "status": "In Progress", "statusCategory": "In Progress", "assigneeAvatarUrl": "https://...", "commitCount": 2 }
  ],
  "jiraOnly": [
    { "key": "PROJ-105", "summary": "Add dark mode toggle", "status": "To Do", "statusCategory": "To Do", "assigneeAvatarUrl": null, "commitCount": 0 }
  ],
  "gitOnly": [
    { "key": "PROJ-111", "summary": null, "status": null, "statusCategory": null, "assigneeAvatarUrl": null, "commitCount": 1 }
  ],
  "unmatchedCommits": [
    { "sha": "abc1234", "authorName": "Jane Smith", "message": "wip: quick fix for prod" }
  ],
  "lastSyncedAt": "2026-05-17T10:00:00Z"
}
```

**Unsupported repository (non-semver tag)**:
```json
{
  "repositoryId": 12,
  "repositoryName": "Deploy.Scripts",
  "currentTag": "release-2026-05-01",
  "nextVersion": null,
  "jiraFixVersionName": null,
  "jiraFixVersionExists": false,
  "supported": false,
  "unsupportedReason": "Latest tag 'release-2026-05-01' is not a semver tag (MAJOR.MINOR.PATCH).",
  "counts": { "commitCount": 0, "gitTicketCount": 0, "jiraTicketCount": 0, "inBothCount": 0, "jiraOnlyCount": 0, "gitOnlyCount": 0 },
  "matchRate": 0.0,
  "health": "Unknown",
  "inBoth": [], "jiraOnly": [], "gitOnly": [], "unmatchedCommits": [],
  "lastSyncedAt": "2026-05-17T10:00:00Z"
}
```

| Status | Condition |
|--------|-----------|
| 200 | Coverage data returned (supported or unsupported) |
| 404 | Repository not found |

---

### GET /projects/{id}/jira-coverage

**Auth**: `[Authorize]` (Admin and Viewer)
**Query params**: `?refresh=true` (optional, default `false`) — forces re-sync for all repos in the project.
**Description**: Returns the Jira coverage summary for all repositories assigned to the given project, plus a project-level aggregate. Repos are returned sorted by `matchRate` ascending (worst first) when `supported = true`; unsupported repos are appended at the end sorted alphabetically.

```json
// Response 200
{
  "projectId": 3,
  "projectName": "Platform Services",
  "totalRepoCount": 5,
  "greenRepoCount": 2,
  "attentionRepoCount": 3,
  "projectMatchRate": 0.782,
  "repos": [
    // Array of RepoJiraComparisonDto — same shape as GET /repositories/{id}/jira-coverage
    // Sorted by matchRate ascending (unsupported repos at the end)
  ]
}
```

| Status | Condition |
|--------|-----------|
| 200 | Coverage data returned |
| 404 | Project not found |

---

### POST /repositories/{id}/jira-coverage/add-ticket

**Auth**: `[Authorize(Roles = "Admin")]`
**Description**: Adds the specified Jira ticket to the repository's computed next-release fix version (`<RepoName>_<NextVersion>`). If the fix version does not exist in Jira, it is created first. After the Jira update succeeds, the repository's coverage snapshot is invalidated (`LastSyncedAt = DateTime.MinValue`) so the next read triggers a recompute.

```json
// Request body
{ "ticketKey": "PROJ-111" }

// Response 200
{
  "success": true,
  "jiraFixVersionName": "Services.UX_1.31.0",
  "fixVersionCreated": false
}
```

| Status | Condition |
|--------|-----------|
| 200 | Ticket added to fix version |
| 400 | `ticketKey` is missing or blank |
| 404 | Repository not found |
| 409 | Repository's latest tag is non-semver; fix version cannot be computed |
| 422 | Ticket key does not exist in Jira, or the Jira project cannot be determined |

---

## Common HTTP Status Codes (feature-relevant)

| Code | Meaning |
|------|---------|
| 200 | Success with body |
| 401 | Missing or invalid JWT |
| 403 | Insufficient role (Viewer attempting Admin action) |
| 404 | Resource not found |
| 409 | Conflict — non-semver tag prevents fix-version computation |
| 422 | Unprocessable — ticket does not exist in Jira |

All error responses follow RFC 7807:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Conflict",
  "status": 409,
  "detail": "Cannot compute next fix version: latest tag 'release-2026-05-01' is not a semver tag.",
  "traceId": "00-abc123-def456-00"
}
```
