# API Contracts: Project Page Templates

**Phase**: 1
**Status**: Final

OpenAPI 3.1 fragments for every endpoint added or changed by this feature. These are the contract that backend controllers (T029, T030, T031) and frontend API clients (T032, T038) implement against. Schemas are defined inline at the bottom of the file.

---

## `GET /api/v1/projects/{projectId}/template-bindings`

Lists every template binding for a project, ordered by `sortOrder` ascending.

**Authorization**: `Viewer` or `Admin`.

**Responses**:

- `200 OK` → `BindingDto[]`
- `404 Not Found` → project does not exist.

```yaml
get:
  summary: List template bindings for a project
  parameters:
    - in: path
      name: projectId
      schema: { type: string, format: uuid }
      required: true
  responses:
    '200':
      description: Bindings ordered by sortOrder ascending
      content:
        application/json:
          schema:
            type: array
            items: { $ref: '#/components/schemas/BindingDto' }
    '404':
      $ref: '#/components/responses/NotFound'
```

---

## `POST /api/v1/projects/{projectId}/template-bindings`

Creates a new binding. Append to the end (`sortOrder = max + 1`) unless caller provides one.

**Authorization**: `Admin`.

**Responses**:

- `201 Created` → `BindingDto`, with `Location` header.
- `400 Bad Request` → validation failure (empty title template, kind invalid, etc.).
- `409 Conflict` → duplicate `(ProjectId, TemplateId, Kind)`.

```yaml
post:
  summary: Create a template binding
  parameters:
    - in: path
      name: projectId
      schema: { type: string, format: uuid }
      required: true
  requestBody:
    required: true
    content:
      application/json:
        schema: { $ref: '#/components/schemas/CreateBindingDto' }
  responses:
    '201':
      description: Created
      headers:
        Location:
          schema: { type: string }
      content:
        application/json:
          schema: { $ref: '#/components/schemas/BindingDto' }
    '400':
      $ref: '#/components/responses/ValidationError'
    '409':
      $ref: '#/components/responses/Conflict'
```

---

## `PATCH /api/v1/projects/{projectId}/template-bindings/{bindingId}`

Partially updates a binding. Any subset of fields may be supplied.

**Authorization**: `Admin`.

**Responses**:

- `200 OK` → updated `BindingDto`.
- `400 Bad Request` → validation failure.
- `404 Not Found`.
- `409 Conflict` → update would create a duplicate or leave the project without a `ReleaseNotes` binding.

```yaml
patch:
  summary: Update a template binding
  parameters:
    - in: path
      name: projectId
      schema: { type: string, format: uuid }
      required: true
    - in: path
      name: bindingId
      schema: { type: string, format: uuid }
      required: true
  requestBody:
    required: true
    content:
      application/json:
        schema: { $ref: '#/components/schemas/UpdateBindingDto' }
  responses:
    '200':
      content:
        application/json:
          schema: { $ref: '#/components/schemas/BindingDto' }
    '400': { $ref: '#/components/responses/ValidationError' }
    '404': { $ref: '#/components/responses/NotFound' }
    '409': { $ref: '#/components/responses/Conflict' }
```

---

## `DELETE /api/v1/projects/{projectId}/template-bindings/{bindingId}`

Removes a binding.

**Authorization**: `Admin`.

**Responses**:

- `204 No Content`.
- `404 Not Found`.
- `409 Conflict` → the binding is the last `ReleaseNotes` binding for the project (FR-002).

---

## `PUT /api/v1/projects/{projectId}/template-bindings/order`

Bulk reorder.

**Authorization**: `Admin`.

**Request**:

```json
{ "orderedIds": ["<bindingId-1>", "<bindingId-2>", "..."] }
```

The list must contain every existing binding id for the project exactly once.

**Responses**:

- `200 OK` → `BindingDto[]` (new order).
- `400 Bad Request` → list does not match the project's bindings.

---

## `POST /api/v1/projects/{projectId}/releases/preview`

Builds the `ReleaseRenderContext`, renders every binding, and returns a `PreparedReleaseDto`. Side-effect-free.

**Authorization**: `Admin`.

**Request** (optional body):

```json
{ "versionOverride": "1.31.0", "reconciliation": { /* ReconciliationSummaryDto */ } }
```

- `versionOverride` — when provided, used in place of the auto-resolved version (FR-022).
- `reconciliation` — when provided, populates the `{{reconciliation}}` slot (FR-015).

**Responses**:

- `200 OK` → `PreparedReleaseDto`.
- `400 Bad Request` → version override fails validation (`^\d+\.\d+\.\d+$`).
- `409 Conflict` → project has no `ReleaseNotes` binding (FR-018) **or** version-primary repo has no tag and no `versionOverride` was provided (FR-022).

```yaml
post:
  summary: Preview a release (render all bound pages with auto-resolved context)
  parameters:
    - in: path
      name: projectId
      schema: { type: string, format: uuid }
      required: true
  requestBody:
    required: false
    content:
      application/json:
        schema: { $ref: '#/components/schemas/PreviewRequestDto' }
  responses:
    '200':
      content:
        application/json:
          schema: { $ref: '#/components/schemas/PreparedReleaseDto' }
    '400': { $ref: '#/components/responses/ValidationError' }
    '409': { $ref: '#/components/responses/Conflict' }
```

---

## `POST /api/v1/projects/{projectId}/releases` (extended)

Existing endpoint, extended to accept `pages[]`. Backward-compatibility: if the body contains a legacy `notes` field and no `pages`, the server treats `notes` as the body of the `ReleaseNotes` page and uses fresh-rendered defaults for the rest.

**Request**:

```json
{
  "version": "1.31.0",
  "pages": [
    { "kind": "ReleaseNotes", "title": "Payments 1.31.0 — Release Notes", "body": "..." },
    { "kind": "Checklist",    "title": "Payments 1.31.0 — Smoke Tests",   "body": "..." }
  ],
  "reconciliation": { /* optional ReconciliationSummaryDto */ }
}
```

**Responses**:

- `201 Created` → `ReleaseDto` with both Confluence URLs populated.
- `400 Bad Request` → title or body validation failure (FR-011, FR-012).
- `409 Conflict` → project has no `ReleaseNotes` binding.

---

## Schemas

```yaml
components:
  schemas:

    BindingDto:
      type: object
      required: [id, projectId, templateId, kind, pageTitleTemplate, linkFromReleaseNotes, sortOrder, createdAtUtc, updatedAtUtc]
      properties:
        id:                    { type: string, format: uuid }
        projectId:             { type: string, format: uuid }
        templateId:            { type: string, format: uuid }
        kind:                  { type: string, enum: [ReleaseNotes, Checklist, Custom] }
        pageTitleTemplate:     { type: string, maxLength: 500 }
        parentPageIdOverride:  { type: string, nullable: true }
        linkFromReleaseNotes:  { type: boolean }
        sortOrder:             { type: integer, minimum: 0 }
        createdAtUtc:          { type: string, format: date-time }
        updatedAtUtc:          { type: string, format: date-time }

    CreateBindingDto:
      type: object
      required: [templateId, kind, pageTitleTemplate]
      properties:
        templateId:            { type: string, format: uuid }
        kind:                  { type: string, enum: [ReleaseNotes, Checklist, Custom] }
        pageTitleTemplate:     { type: string, maxLength: 500 }
        parentPageIdOverride:  { type: string, nullable: true }
        linkFromReleaseNotes:  { type: boolean, default: false }
        sortOrder:             { type: integer, minimum: 0, nullable: true }

    UpdateBindingDto:
      type: object
      properties:
        kind:                  { type: string, enum: [ReleaseNotes, Checklist, Custom] }
        pageTitleTemplate:     { type: string, maxLength: 500 }
        parentPageIdOverride:  { type: string, nullable: true }
        linkFromReleaseNotes:  { type: boolean }
        sortOrder:             { type: integer, minimum: 0 }

    PreviewRequestDto:
      type: object
      properties:
        versionOverride:       { type: string, pattern: '^\d+\.\d+\.\d+$' }
        reconciliation:        { $ref: '#/components/schemas/ReconciliationSummaryDto' }

    PreparedReleaseDto:
      type: object
      required: [context, pages]
      properties:
        context:               { $ref: '#/components/schemas/ReleaseRenderContextDto' }
        pages:
          type: array
          items: { $ref: '#/components/schemas/PreparedPageDto' }
        warnings:
          type: array
          items: { type: string }

    PreparedPageDto:
      type: object
      required: [kind, title, body, parentPageId, linkFromReleaseNotes, sortOrder]
      properties:
        kind:                  { type: string, enum: [ReleaseNotes, Checklist, Custom] }
        title:                 { type: string }
        body:                  { type: string }
        parentPageId:          { type: string }
        linkFromReleaseNotes:  { type: boolean }
        sortOrder:             { type: integer }

    ReleaseRenderContextDto:
      type: object
      required: [project, version, previousVersion, releaseDate, repositories, tickets, contributors, confluence, custom]
      properties:
        project:               { $ref: '#/components/schemas/ProjectContextDto' }
        version:               { type: string }
        previousVersion:       { type: string }
        releaseDate:           { type: string, format: date-time }
        repositories:
          type: array
          items: { $ref: '#/components/schemas/RepoContextDto' }
        tickets:               { $ref: '#/components/schemas/TicketBucketsDto' }
        contributors:
          type: array
          items: { $ref: '#/components/schemas/ContributorContextDto' }
        reconciliation:
          oneOf:
            - { $ref: '#/components/schemas/ReconciliationSummaryDto' }
            - { type: 'null' }
        confluence:            { $ref: '#/components/schemas/ConfluenceContextDto' }
        custom:
          type: object
          additionalProperties: { type: string }

    ProjectContextDto:
      type: object
      properties:
        name:                  { type: string }
        key:                   { type: string }
        description:           { type: string }
        badgeColor:            { type: string }

    RepoContextDto:
      type: object
      properties:
        name:                  { type: string }
        slug:                  { type: string }
        previousTag:           { type: string }
        nextTag:               { type: string }
        compareUrl:            { type: string, format: uri }
        jiraFixVersionName:    { type: string }
        commitCount:           { type: integer }
        ticketCount:           { type: integer }
        hasBreakingChanges:    { type: boolean }

    TicketBucketsDto:
      type: object
      properties:
        breaking: { type: array, items: { $ref: '#/components/schemas/TicketContextDto' } }
        features: { type: array, items: { $ref: '#/components/schemas/TicketContextDto' } }
        fixes:    { type: array, items: { $ref: '#/components/schemas/TicketContextDto' } }
        other:    { type: array, items: { $ref: '#/components/schemas/TicketContextDto' } }

    TicketContextDto:
      type: object
      properties:
        key:                   { type: string }
        summary:               { type: string }
        dominantChangeType:    { type: string, enum: [breaking, feat, fix, chore, docs, refactor, perf, test, build, ci, style, revert, unconventional] }
        commitCount:           { type: integer }
        contributors:
          type: array
          items: { type: string }
        jiraUrl:               { type: string, format: uri }

    ContributorContextDto:
      type: object
      properties:
        name:                  { type: string }
        email:                 { type: string }
        commitCount:           { type: integer }

    ReconciliationSummaryDto:
      type: object
      properties:
        matched:               { type: integer }
        jiraOnly:              { type: integer }
        gitOnly:               { type: integer }
        matchRatePercent:      { type: number, format: float }
        capturedHash:          { type: string }

    ConfluenceContextDto:
      type: object
      properties:
        spaceKey:              { type: string }
        parentPageId:          { type: string }
        parentPageUrl:         { type: string, format: uri }

  responses:
    NotFound:
      description: Resource not found
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ProblemDetails' }
    ValidationError:
      description: Validation failed
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ProblemDetails' }
    Conflict:
      description: Conflict
      content:
        application/json:
          schema: { $ref: '#/components/schemas/ProblemDetails' }
```

`ProblemDetails` follows RFC 7807 and is already defined elsewhere in the platform's OpenAPI spec.

---

## Backward-compatibility test surface

Two compatibility contracts must hold:

1. The legacy `POST /api/v1/projects/{id}/releases` shape (with a single `notes` string and no `pages[]`) MUST continue to succeed and produce a single release-notes page using the project's seeded `ReleaseNotes` binding. The integration test `PreviewAndPublishFlowTests.LegacySingleNotesBodyStillPublishes` covers this.

2. The legacy GET `/api/v1/projects/{id}` response MUST continue to include `defaultReleaseNoteTemplateId` for clients that pre-date this feature. The field is marked `deprecated: true` in the OpenAPI spec and will be removed when `Project.DefaultReleaseNoteTemplateId` itself is dropped.
