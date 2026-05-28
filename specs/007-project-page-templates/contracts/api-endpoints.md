# API Contracts: Project Page Templates

**Branch**: `007-project-page-templates`
**Date**: 2026-05-24
**Base path**: `/api/v1`

All endpoints require `Authorization: Bearer <jwt>`. Write endpoints require `Admin` role. Read endpoints allow `Viewer` role.

---

## Template Bindings

### GET /projects/{projectId}/template-bindings

List all template bindings for a project, ordered by `SortOrder`.

**Auth**: Viewer

**Response 200**:
```json
[
  {
    "id": 1,
    "projectId": 42,
    "templateId": 3,
    "templateName": "Standard Release Notes",
    "kind": "ReleaseNotes",
    "pageTitleTemplate": "{{project.name}} {{version}} — Release Notes",
    "parentPageId": null,
    "linkFromReleaseNotes": false,
    "sortOrder": 0
  },
  {
    "id": 2,
    "projectId": 42,
    "templateId": 7,
    "templateName": "Smoke Test Checklist",
    "kind": "Checklist",
    "pageTitleTemplate": "{{project.name}} {{version}} — Smoke Tests",
    "parentPageId": "12345678",
    "linkFromReleaseNotes": true,
    "sortOrder": 1
  }
]
```

**Response 404**: Project not found.

---

### POST /projects/{projectId}/template-bindings

Create a new template binding for a project.

**Auth**: Admin

**Request body**:
```json
{
  "templateId": 7,
  "kind": "Checklist",
  "pageTitleTemplate": "{{project.name}} {{version}} — Smoke Tests",
  "parentPageId": "12345678",
  "linkFromReleaseNotes": true,
  "sortOrder": 1
}
```

**Validation**:
- `templateId`: required, must reference an existing `ReleaseNoteTemplate`.
- `kind`: required, one of `ReleaseNotes | Checklist | Custom`.
- `pageTitleTemplate`: required, 1–500 chars.
- `sortOrder`: required, ≥ 0.

**Response 201**: Created binding (same shape as list item above).
**Response 400**: Validation failure (RFC 7807 ProblemDetails).
**Response 404**: Project or template not found.
**Response 409**: `kind = ReleaseNotes` and a `ReleaseNotes` binding already exists for this project.

---

### PUT /projects/{projectId}/template-bindings/{bindingId}

Update an existing binding.

**Auth**: Admin

**Request body** (all fields optional; omitted fields unchanged):
```json
{
  "templateId": 7,
  "kind": "Checklist",
  "pageTitleTemplate": "{{project.name}} {{version}} — Smoke Tests",
  "parentPageId": "12345678",
  "linkFromReleaseNotes": true,
  "sortOrder": 1
}
```

**Response 200**: Updated binding.
**Response 400**: Validation failure.
**Response 404**: Project or binding not found.
**Response 409**: Update would violate the single-`ReleaseNotes`-per-project constraint.

---

### DELETE /projects/{projectId}/template-bindings/{bindingId}

Delete a template binding.

**Auth**: Admin

**Response 204**: Deleted.
**Response 404**: Project or binding not found.
**Response 409**: `conflict_code: "last_release_notes_binding"` — cannot delete the only `ReleaseNotes` binding.

---

### POST /projects/{projectId}/template-bindings/reorder

Reorder template bindings atomically.

**Auth**: Admin

**Request body**:
```json
{
  "orderedIds": [2, 1, 3]
}
```

`orderedIds` must contain exactly the same binding IDs as currently exist for the project (no additions, no omissions). The system assigns `SortOrder` 0, 1, 2, … in the given order.

**Response 200**: Full list of updated bindings (same shape as GET list).
**Response 400**: `orderedIds` is missing bindings or contains unknown IDs.
**Response 404**: Project not found.

---

## Project Custom Variables

### GET /projects/{projectId}/custom-variables

List all custom variables for a project.

**Auth**: Viewer

**Response 200**:
```json
[
  { "key": "slackChannel", "value": "#payments-releases" },
  { "key": "onCallEmail", "value": "oncall@example.com" }
]
```

---

### PUT /projects/{projectId}/custom-variables/{key}

Create or update a single custom variable (upsert).

**Auth**: Admin

**Request body**:
```json
{ "value": "#payments-releases" }
```

**Validation**:
- `key` (path param): 1–50 chars, pattern `[a-zA-Z][a-zA-Z0-9_]*`.
- `value`: 0–500 chars.

**Response 200**: `{ "key": "slackChannel", "value": "#payments-releases" }`.
**Response 400**: Validation failure.

---

### DELETE /projects/{projectId}/custom-variables/{key}

Delete a custom variable.

**Auth**: Admin

**Response 204**: Deleted.
**Response 404**: Key not found for this project.

---

## Project Settings — Version Bump Strategy

### PATCH /projects/{projectId}

Existing endpoint — extended to accept `versionBumpStrategy`.

**Auth**: Admin

**Additional request field**:
```json
{ "versionBumpStrategy": "Minor" }
```

Values: `Patch | Minor | Major`. Omit to leave unchanged.

**Response 200**: Updated project.
**Response 400**: Invalid value.

---

## Release Wizard — Prepare & Publish Pages

### POST /releases/{releaseId}/prepare-pages

Render all bound templates for the release's project and return the prepared pages. Accepts optional reconciliation data to populate the `{{reconciliation}}` context variable.

**Auth**: Admin

**Request body**:
```json
{
  "reconciliationData": null
}
```

`reconciliationData` is nullable. When null, `{{reconciliation}}` context is null (blocks guarded by `{{#if reconciliation}}` render nothing).

When provided:
```json
{
  "reconciliationData": {
    "matchedCount": 18,
    "jiraOnlyCount": 2,
    "gitOnlyCount": 1,
    "matchRate": 0.857,
    "runAt": "2026-05-24T14:30:00Z"
  }
}
```

**Validation**:
- `releaseId` must exist.
- Project must have at least one binding of kind `ReleaseNotes` (else `400` with `conflict_code: "no_release_notes_binding"`).
- Version-primary repository must have at least one semver tag (else `400` with `conflict_code: "no_semver_tag"` and `"adminOverrideVersion"` hint).

**Response 200**:
```json
{
  "context": {
    "project": { "id": 42, "name": "Payments", "description": "..." },
    "version": "1.31.0",
    "previousVersion": "1.30.0",
    "releaseDate": "2026-05-24T00:00:00Z",
    "repositories": [
      {
        "name": "payments-api",
        "previousTag": "1.30.0",
        "nextTag": "1.31.0",
        "commitCount": 12,
        "ticketCount": 8,
        "jiraFixVersion": "payments-api_1.31.0"
      }
    ],
    "tickets": {
      "breaking": [],
      "features": [{ "id": "PAY-123", "summary": "Add refund flow" }],
      "fixes": [],
      "other": []
    },
    "contributors": [{ "name": "Alice", "commitCount": 7 }],
    "reconciliation": null,
    "confluence": { "spaceKey": "REL", "parentPageId": "98765432" },
    "custom": { "slackChannel": "#payments-releases" }
  },
  "pages": [
    {
      "bindingId": 1,
      "kind": "ReleaseNotes",
      "title": "Payments 1.31.0 — Release Notes",
      "body": "...(rendered Handlebars output)...",
      "parentPageId": "98765432",
      "linkFromReleaseNotes": false,
      "sortOrder": 0,
      "unknownTokens": []
    },
    {
      "bindingId": 2,
      "kind": "Checklist",
      "title": "Payments 1.31.0 — Smoke Tests",
      "body": "...",
      "parentPageId": "12345678",
      "linkFromReleaseNotes": true,
      "sortOrder": 1,
      "unknownTokens": ["custom.slakChannel"]
    }
  ],
  "warnings": [
    "Page 2 references unknown template variable: custom.slakChannel"
  ]
}
```

**Response 400**:
- `conflict_code: "no_release_notes_binding"` — wizard redirect to Settings → Projects → Pages.
- `conflict_code: "no_semver_tag"` — include `adminOverrideVersion` field in `POST /releases/{id}/prepare-pages` body.

**Admin override** (when version-primary repo has no semver tag):
```json
{
  "adminOverrideVersion": "1.0.0",
  "reconciliationData": null
}
```

---

### POST /releases/{releaseId}/publish-pages

Publish prepared pages to Confluence. Accepts the client's (potentially edited) page list.

**Auth**: Admin

**Request body**:
```json
{
  "pages": [
    {
      "bindingId": 1,
      "title": "Payments 1.31.0 — Release Notes",
      "body": "...(may contain user edits)...",
      "parentPageId": "98765432",
      "linkFromReleaseNotes": false,
      "sortOrder": 0
    },
    {
      "bindingId": 2,
      "title": "Payments 1.31.0 — Smoke Tests",
      "body": "...",
      "parentPageId": "12345678",
      "linkFromReleaseNotes": true,
      "sortOrder": 1
    }
  ]
}
```

**Validation** (server-side, authoritative — client mirrors these in Zod):
- `pages` must be non-empty.
- Every `title` must be 1–255 chars (non-empty, not exceeding Confluence limit).
- `pages` must contain exactly one entry with `sortOrder` matching the `ReleaseNotes` binding's sort order.
- `bindingId` values must correspond to active bindings for the release's project.

**Response 200**:
```json
{
  "publishedPages": [
    {
      "bindingId": 1,
      "confluencePageId": "11223344",
      "confluenceUrl": "https://example.atlassian.net/wiki/spaces/REL/pages/11223344",
      "title": "Payments 1.31.0 — Release Notes"
    },
    {
      "bindingId": 2,
      "confluencePageId": "11223355",
      "confluenceUrl": "https://example.atlassian.net/wiki/spaces/REL/pages/11223355",
      "title": "Payments 1.31.0 — Smoke Tests"
    }
  ]
}
```

**Response 400**: Title validation failure (RFC 7807 with per-page error details).
**Response 502**: Confluence API error (mapped from `ExternalServiceException`).

---

## Template Preview — Live Context Selector

### GET /templates/{templateId}/preview

Preview a template with a sample context or a project's latest release context.

**Auth**: Viewer

**Query parameters**:
- `contextSource`: `synthetic` (default) | `project`
- `projectId`: required when `contextSource = project`

**Response 200**:
```json
{
  "renderedTitle": "Payments 1.31.0 — Release Notes",
  "renderedBody": "...",
  "unknownTokens": [],
  "contextSource": "project",
  "projectName": "Payments",
  "releaseVersion": "1.31.0"
}
```

**Response 400**: `contextSource = project` but `projectId` missing.
**Response 404**: Template or project not found.
