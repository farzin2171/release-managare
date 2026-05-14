# API Endpoints Contract

**Base path**: `/api/v1`  
**Auth**: JWT Bearer (all endpoints unless noted)  
**Error format**: RFC 7807 ProblemDetails

---

## Auth

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/auth/setup` | None | One-time admin bootstrap; returns `410 Gone` after first use |
| POST | `/auth/login` | None | Returns `{ accessToken, refreshToken, expiresAt }` |
| POST | `/auth/refresh` | None | Rotates refresh token, returns new pair |

### POST /auth/setup
```json
// Request
{ "email": "admin@example.com", "password": "..." }
// Response 201
{ "id": "guid", "email": "...", "role": "Admin" }
```

### POST /auth/login
```json
// Request
{ "email": "...", "password": "..." }
// Response 200
{ "accessToken": "...", "refreshToken": "...", "expiresAt": "ISO8601" }
```

---

## Users  *(Admin only for all write operations)*

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/users` | Admin | List all users |
| POST | `/users` | Admin | Create user |
| PUT | `/users/{id}` | Admin | Update user (role, isActive, password) |
| DELETE | `/users/{id}` | Admin | Deactivate user |

### POST /users
```json
// Request
{ "email": "...", "password": "...", "role": "Admin|Viewer" }
// Response 201
{ "id": "guid", "email": "...", "role": "...", "isActive": true, "createdAt": "..." }
```

---

## Integrations — Git Provider

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/integrations/git/test` | Admin | Test connection without saving |
| GET | `/integrations/git` | Any | List all Git connections |
| POST | `/integrations/git` | Admin | Create Git connection |
| PUT | `/integrations/git/{id}` | Admin | Update Git connection |
| POST | `/integrations/git/{id}/sync` | Admin | Trigger repository sync (async) |

### POST /integrations/git
```json
// Request
{ "name": "My AzDO Org", "providerType": "AzureDevOps", "organizationUrl": "https://dev.azure.com/myorg", "pat": "..." }
// Response 201
{ "id": "guid", "name": "...", "providerType": "AzureDevOps", "organizationUrl": "...", "isActive": true, "lastSyncedAt": null }
```

### POST /integrations/git/{id}/sync
```json
// Response 202 Accepted
{ "message": "Sync started", "connectionId": "guid" }
```

---

## Integrations — Confluence

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/integrations/confluence/test` | Admin | Test without saving |
| GET | `/integrations/confluence` | Any | Get current Confluence connection |
| PUT | `/integrations/confluence` | Admin | Create or update Confluence connection |

---

## Integrations — Jira

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/integrations/jira/test` | Admin | Test without saving |
| GET | `/integrations/jira` | Any | Get current Jira connection |
| PUT | `/integrations/jira` | Admin | Create or update Jira connection |
| GET | `/integrations/jira/projects` | Any | List Jira projects from connection |

---

## Repositories

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/repositories` | Any | List repositories; query: `?connectionId=&isTracked=&search=` |
| PATCH | `/repositories/{id}` | Admin | Toggle `isTracked` |
| GET | `/repositories/{id}/changes` | Any | Changes since last tag; query: `?groupBy=ticket\|commit\|contributor&type=&contributor=&search=` |

### GET /repositories/{id}/changes response
```json
{
  "repositoryId": "guid",
  "repositoryName": "...",
  "fromTag": "v1.2.0",
  "toTag": "HEAD",
  "summary": { "commitCount": 42, "ticketCount": 12, "breakingCount": 1, "contributorCount": 5 },
  "groups": [
    {
      "key": "APPLY-123",
      "title": "Add dark mode support",
      "type": "feat",
      "isBreaking": false,
      "commitCount": 3,
      "contributorCount": 2,
      "commits": [
        { "sha": "abc1234", "shortSha": "abc1234", "message": "...", "author": "...", "committedAt": "..." }
      ]
    }
  ],
  "unscoped": [ /* same commit shape */ ]
}
```

---

## Projects

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/projects` | Any | List all projects |
| POST | `/projects` | Admin | Create project |
| GET | `/projects/{id}` | Any | Get project detail |
| PUT | `/projects/{id}` | Admin | Update project |
| DELETE | `/projects/{id}` | Admin | Delete project |
| POST | `/projects/{id}/repositories/{repoId}` | Admin | Assign repository to project |
| DELETE | `/projects/{id}/repositories/{repoId}` | Admin | Remove repository from project |
| PUT | `/projects/{id}/jira` | Admin | Configure Jira mapping for project |
| GET | `/projects/{id}/changes` | Any | Aggregated changes across all project repos |

### POST /projects
```json
// Request
{ "name": "Apply", "description": "...", "color": "#3B82F6" }
// Response 201
{ "id": "guid", "name": "Apply", "description": "...", "color": "#3B82F6", "createdAt": "..." }
```

### PUT /projects/{id}/jira
```json
// Request
{
  "jiraConnectionId": "guid",
  "jiraProjectKeys": ["APPLY", "CORE"],
  "fixVersionPattern": "Apply {version}",
  "autoCreateFixVersion": true,
  "matchSubtasksToParents": false
}
```

---

## Releases

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/projects/{id}/releases` | Any | Create draft release |
| GET | `/releases/{id}` | Any | Get release detail |
| PUT | `/releases/{id}` | Any | Update edited notes (Draft only) |
| POST | `/releases/{id}/publish` | Any | Publish to Confluence (marks as Published + locked) |
| POST | `/releases/{id}/reconcile` | Any | Run Jira reconciliation |
| GET | `/releases/{id}/reconciliation` | Any | Get latest reconciliation snapshot |
| POST | `/releases/{id}/reconciliation/jira-tickets` | Admin | Add Git-only tickets to Jira fix version |

### POST /projects/{id}/releases request
```json
{
  "version": "1.3.0",
  "templateId": "guid",
  "repositoryTags": [
    { "repositoryId": "guid", "fromTag": "v1.2.0", "toTag": "HEAD" }
  ]
}
```

### GET /releases/{id}/reconciliation response
```json
{
  "releaseId": "guid",
  "runAt": "ISO8601",
  "matchedCount": 18,
  "jiraOnlyCount": 2,
  "gitOnlyCount": 3,
  "matchRatePercent": 78.26,
  "matched": [ { "key": "APPLY-100", "summary": "...", "status": "Done" } ],
  "jiraOnly": [ { "key": "APPLY-101", "summary": "..." } ],
  "gitOnly": [ { "ticketId": "APPLY-102", "title": "...", "commitCount": 1 } ]
}
```

### POST /releases/{id}/reconciliation/jira-tickets request
```json
{ "ticketKeys": ["APPLY-102", "APPLY-103"] }
```

---

## Templates

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/templates` | Any | List all templates |
| POST | `/templates` | Admin | Create template |
| PUT | `/templates/{id}` | Admin | Update template |
| DELETE | `/templates/{id}` | Admin | Delete template |

---

## Health

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/health/live` | None | Liveness probe (process alive) |
| GET | `/health/ready` | None | Readiness probe (DB connected) |

---

## Common HTTP Status Codes

| Code | Meaning |
|------|---------|
| 200 | Success (GET, PUT) |
| 201 | Created (POST resource creation) |
| 202 | Accepted (async operations like sync) |
| 204 | No Content (DELETE) |
| 400 | Validation error (FluentValidation failure) |
| 401 | Missing or invalid JWT |
| 403 | Insufficient role (Viewer attempting Admin action) |
| 404 | Resource not found |
| 409 | Conflict (duplicate name, already published) |
| 410 | Gone (setup endpoint after first use) |
| 500 | Internal server error |

All error responses follow RFC 7807:
```json
{
  "type": "https://tools.ietf.org/html/rfc7807",
  "title": "Not Found",
  "status": 404,
  "detail": "Project with ID 'guid' was not found.",
  "traceId": "correlation-id"
}
```
