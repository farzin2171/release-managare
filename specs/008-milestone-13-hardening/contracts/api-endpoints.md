# API Endpoints: Milestone 13 — Security, Service Ownership & UX Hardening

**Branch**: `009-milestone-13-hardening`
**Date**: 2026-05-30

All endpoints require `Authorization: Bearer <access_token>` unless noted. All error responses follow RFC 7807 ProblemDetails with an additional `code` field.

---

## Modified Endpoints

### 1. `POST /api/v1/auth/setup`

**Change**: Add `SetupKeyAuthorizationFilter`. Returns `401` if key header missing/wrong; returns `409` if setup already complete.

**New required header**:
```
X-Setup-Key: <value>
```

**Request body** (unchanged):
```json
{
  "username": "admin",
  "password": "s3cr3t"
}
```

**Responses**:

| Status | Body | Condition |
|--------|------|-----------|
| `201 Created` | `{ "id": 1, "username": "admin", "role": "Admin" }` | Key correct, no existing users |
| `401 Unauthorized` | `{ "code": "setup_key_invalid" }` | Header absent or value incorrect |
| `409 Conflict` | `{ "code": "setup_already_complete" }` | At least one user already exists |

**Auth**: None (endpoint is pre-auth by design; protected by setup key instead).

**OpenAPI note**: The `X-Setup-Key` header MUST be excluded from the Swagger UI and OpenAPI specification. Apply `[ApiExplorerSettings(IgnoreApi = false)]` with a custom `IDocumentFilter` or mark the parameter with `[SwaggerIgnore]`.

---

### 2. `GET /api/v1/repositories/{id}`

**Change**: Response now includes `serviceOwner` field.

**Response** (addition only — non-breaking):
```json
{
  "id": 42,
  "name": "payment-service",
  "url": "https://...",
  "serviceOwner": "Platform Team",
  ...
}
```

`serviceOwner` is `null` when not set.

---

### 3. `PUT /api/v1/repositories/{id}`

**Change**: Request body now accepts `serviceOwner`.

**Request body** (addition):
```json
{
  "name": "payment-service",
  "url": "https://...",
  "serviceOwner": "Platform Team"
}
```

`serviceOwner` is optional. Pass `null` or omit to clear.

**Validation**: max 120 characters.

**Auth**: `[Authorize(Roles = "Admin")]`

---

### 4. `GET /api/v1/templates`

**Change**: Response now includes `isSystem` flag on each template.

**Response** (addition only):
```json
[
  {
    "id": 1,
    "name": "Release Summary (Default)",
    "isSystem": true,
    ...
  },
  {
    "id": 2,
    "name": "My Custom Template",
    "isSystem": false,
    ...
  }
]
```

---

### 5. `PUT /api/v1/templates/{id}`

**Change**: Returns `403` when `isSystem = true`.

**New error response**:

| Status | Body | Condition |
|--------|------|-----------|
| `403 Forbidden` | `{ "code": "system_template_readonly" }` | `isSystem = true` on the target template |

All other behaviour unchanged.

**Auth**: `[Authorize(Roles = "Admin")]`

---

### 6. `DELETE /api/v1/templates/{id}`

**Change**: Returns `403` when `isSystem = true`.

**New error response**:

| Status | Body | Condition |
|--------|------|-----------|
| `403 Forbidden` | `{ "code": "system_template_readonly" }` | `isSystem = true` on the target template |

**Auth**: `[Authorize(Roles = "Admin")]`

---

### 7. `POST /api/v1/auth/refresh`

**Change**: Response now sets the refresh token as an httpOnly cookie instead of (or in addition to) returning it in the JSON body.

**Set-Cookie response header** (new):
```
Set-Cookie: refreshToken=<value>; HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth; Max-Age=2592000
```

**Request**: The refresh token is now read from the `refreshToken` cookie. The request body becomes empty or contains only a CSRF token if needed.

**Response body** (unchanged):
```json
{
  "accessToken": "<new JWT>",
  "expiresIn": 28800
}
```

**Auth**: None (the refresh token in the cookie authenticates this request).

---

## New Endpoints

### 8. `POST /api/v1/templates/{id}/clone`

**Purpose**: Create an editable copy of any template (intended primarily for system templates that cannot be edited in-place).

**Request**: No body.

**Response `201 Created`**:
```json
{
  "id": 5,
  "name": "Release Summary (Default) (copy)",
  "body": "...",
  "isSystem": false,
  "createdAt": "2026-05-30T10:00:00Z"
}
```

**Clone naming rule**: The clone is named `"<original name> (copy)"`. If that name is already taken, the system tries `"<original name> (copy 2)"`, `"(copy 3)"`, etc., until a unique name is found (cap: 100 attempts, then `409 Conflict`).

**Error responses**:

| Status | Body | Condition |
|--------|------|-----------|
| `404 Not Found` | `{ "code": "template_not_found" }` | Template with given ID does not exist |
| `409 Conflict` | `{ "code": "clone_name_exhausted" }` | 100 copy suffixes all taken (pathological edge case) |

**Auth**: `[Authorize(Roles = "Admin")]`

---

## `GET /api/v1/releases/{id}` — no change

The `DELETE /api/projects/{projectId}/releases/{id}` endpoint already exists and already returns `409` for non-Draft releases. No backend change is needed for Feature E; it is a pure frontend addition.
