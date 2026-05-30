# Spec Addendum — Milestone 13: Security, Service Ownership & UX Hardening

> Paste each section into the corresponding Spec Kit slash command in Claude Code.
> Run after Milestone 12 is complete and its smoke test passes.

---

## `/specify` addendum

### Feature A — API-Key protection for the `/auth/setup` endpoint

**Context:** The first-run admin bootstrap endpoint (`POST /api/v1/auth/setup`) is currently self-disabling after first use, but is unauthenticated and publicly reachable until then. In multi-tenant or cloud deployments this is an attack surface — anyone who hits it before the admin does can claim ownership.

**Requirement:**

The `/auth/setup` endpoint must require a pre-shared **setup API key** supplied by the operator before any user account exists in the database.

- The setup key is a random string (minimum 32 characters) configured via the environment variable `RELEASE_MANAGER_SETUP_KEY`.
- The caller must pass it as an `X-Setup-Key: <value>` request header.
- If the header is absent or the value does not match, the endpoint returns `401 Unauthorized` with body `{ "code": "setup_key_invalid" }`.
- If the key is present and correct but a user already exists (i.e. setup already completed), the endpoint returns `409 Conflict` with body `{ "code": "setup_already_complete" }`.
- The key must never appear in logs, health endpoints, or OpenAPI documentation. Redact it from Serilog destructuring via a custom destructuring policy.
- If `RELEASE_MANAGER_SETUP_KEY` is not set at application startup and no users exist yet, the application must **refuse to start** and log a fatal message: `"RELEASE_MANAGER_SETUP_KEY must be set before first run."` This prevents a misconfigured deployment from leaving setup unprotected.
- Once at least one user exists the variable may be removed from the environment; the endpoint remains permanently disabled (existing behaviour unchanged).

**Acceptance criteria:**
1. `POST /api/v1/auth/setup` without the header → `401`.
2. `POST /api/v1/auth/setup` with a wrong key → `401`.
3. `POST /api/v1/auth/setup` with the correct key and no users → `201 Created`, admin account is created.
4. Any subsequent call to `/auth/setup` → `409` regardless of the key.
5. App fails to start when `RELEASE_MANAGER_SETUP_KEY` is unset and the database is empty.

---

### Feature B — `service_owner` field on Repository

**Context:** Teams need to know who owns each service in a release at a glance, both on the Repositories screen and in generated Confluence pages.

**Requirement:**

Add a nullable `ServiceOwner` string field to the `Repository` entity.

- Displayed and editable in **Settings → Repositories** as a plain text input next to the existing repository metadata. Label: **"Service Owner"**. Placeholder: `"e.g. Platform Team"`.
- Maximum length: 120 characters.
- The field is optional; existing repositories default to `null` (displayed as `—` in the UI).
- `ServiceOwner` is included in the `RepositoryDto` returned by all existing repository endpoints.
- Admin role can edit; Viewer role sees it read-only.
- `ServiceOwner` is exposed in the Handlebars template context as `repo.serviceOwner` (string or empty string when null, never `null` itself in the template).

**Acceptance criteria:**
1. Admin can set and clear `ServiceOwner` from the Repositories screen.
2. The value persists across refreshes.
3. `GET /api/v1/repositories/{id}` returns `"serviceOwner": "Platform Team"`.
4. Template variable `{{repo.serviceOwner}}` renders correctly in a test render.

---

### Feature C — Default "Release Summary" Confluence page template

**Context:** The existing template system (Milestone 12) lets admins bind arbitrary Handlebars templates to a project. A commonly needed page is a **Release Summary** that lists every repository included in the release, its previous and next versions, and its service owner in a table — giving stakeholders a single-glance view.

**Requirement:**

Seed the system with a built-in, read-only template named **"Release Summary (Default)"** that is automatically available on every new installation. It is a system template: admins can clone it (creating an editable copy) but cannot edit or delete the original.

The template produces a Confluence page in **Storage Format** with the following structure:

```
<release.projectName> — <release.name> — Release Summary
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[Info panel]
Released on <release.releaseDate | formatDate "MMMM dd, yyyy">

[Table: one row per repo in release.repositories]
| Repository         | Service Owner   | Previous Version | Next Version | Commits | Tickets |
|--------------------|-----------------|------------------|--------------|---------|---------|
| <repo.name>        | <repo.owner|"—"> | <repo.prevVer>  | <repo.nextVer>| <n>    | <n>    |

[Footer paragraph]
Generated by Release Manager · <release.releaseDate | formatDate "yyyy-MM-dd HH:mm"> UTC
```

**Default Handlebars source** (stored verbatim in the seed migration as `IsSystem = true`):

```handlebars
<ac:structured-macro ac:name="info">
  <ac:rich-text-body>
    <p>Released on <strong>{{formatDate releaseDate "MMMM dd, yyyy"}}</strong></p>
  </ac:rich-text-body>
</ac:structured-macro>

<table>
  <thead>
    <tr>
      <th>Repository</th>
      <th>Service Owner</th>
      <th>Previous Version</th>
      <th>Next Version</th>
      <th>Commits</th>
      <th>Tickets</th>
    </tr>
  </thead>
  <tbody>
    {{#each repositories}}
    <tr>
      <td><strong>{{name}}</strong></td>
      <td>{{#if serviceOwner}}{{serviceOwner}}{{else}}—{{/if}}</td>
      <td><code>{{previousVersion}}</code></td>
      <td><code>{{nextVersion}}</code></td>
      <td>{{commitCount}}</td>
      <td>{{ticketCount}}</td>
    </tr>
    {{/each}}
  </tbody>
</table>

<p><em>Generated by Release Manager · {{formatDate releaseDate "yyyy-MM-dd HH:mm"}} UTC</em></p>
```

**Template context additions** (extending `ReleaseRenderContext`):

| Variable | Type | Source |
|---|---|---|
| `release.repositories` | `IReadOnlyList<RepoSummary>` | `ReleaseRepositories` join rows |
| `repo.serviceOwner` | `string` (empty when null) | `Repository.ServiceOwner` |
| `repo.name` | `string` | `Repository.Name` |
| `repo.previousVersion` | `string` | snapshot `PreviousVersion` |
| `repo.nextVersion` | `string` | snapshot `NextVersion` |
| `repo.commitCount` | `int` | snapshot `CommitCount` |
| `repo.ticketCount` | `int` | snapshot `TicketCount` |

These variables must be available in **all** templates going forward, not only the system template.

**Settings → Templates UI changes:**
- System templates are shown with a `[System]` badge and no Edit/Delete buttons.
- A **"Clone"** button creates an editable copy named `"<original name> (copy)"`.
- New installations: the "Release Summary (Default)" template is pre-bound to every new project as a second `ProjectTemplateBinding` (kind `ReleaseSummary`, `SortOrder = 1`), after the existing `ReleaseNotes` binding.

**Acceptance criteria:**
1. `GET /api/v1/templates` returns the system template with `"isSystem": true`.
2. System template cannot be `PUT` or `DELETE`d — returns `403 Forbidden` with `{ "code": "system_template_readonly" }`.
3. Cloning produces an editable copy.
4. Preview render for a 3-repo release returns a page body containing a `<table>` with 3 data rows, each with the correct `serviceOwner` value.
5. New project created via the UI has the system template auto-bound.

---

### Feature D — JWT Refresh Token flow in the frontend

**Context:** The backend already issues refresh tokens (8-hour JWT + refresh-token rotation, Milestone 1). The frontend currently does not use them — when the access token expires users are silently redirected to the login page, losing unsaved work.

**Requirement:**

The frontend API client must transparently refresh the access token before it expires.

- On every authenticated response with HTTP `401 Unauthorized`, the client attempts one silent refresh call: `POST /api/v1/auth/refresh` with the stored refresh token.
- If the refresh succeeds, the original request is retried exactly once with the new access token. The user notices nothing.
- If the refresh fails (refresh token expired, revoked, or server error), the auth store is cleared and the user is redirected to `/login` with a toast: `"Your session has expired. Please log in again."`
- Proactive refresh: 2 minutes before the access token expiry (decoded from the JWT `exp` claim), the client silently calls `POST /api/v1/auth/refresh` in the background. This prevents expiry mid-action for users with long-running wizard flows.
- Refresh token is stored in an `httpOnly` cookie (set by the backend) — the frontend must not store it in `localStorage` or JS-accessible state. If `httpOnly` cookie behaviour was not already implemented in Milestone 1, implement it now as part of this feature.
- Concurrent requests during a refresh are queued and replayed after the new token arrives (no thundering herd).

**Implementation notes (non-prescriptive guidance for Claude Code):**
- Intercept using a TanStack Query / Axios interceptor pattern, not per-request logic.
- Use a single in-flight refresh promise shared across all queued requests.
- The proactive timer resets on every successful refresh.

**Acceptance criteria:**
1. With a near-expired token (manipulate expiry in dev), making an API call triggers a silent refresh and the call succeeds.
2. With an expired refresh token, the user is redirected to `/login` with the toast message.
3. Two simultaneous API calls during a refresh do not produce two refresh requests.
4. The refresh token is not accessible via `document.cookie` (httpOnly).

---

### Feature E — Delete Draft Releases

**Context:** The `DELETE /api/projects/{projectId}/releases/{id}` endpoint already exists and rejects non-Draft releases with `409`. However, the frontend release list and release detail page have no Delete button exposed to the user.

**Requirement:**

Expose the delete action in the UI for Draft releases, Admin role only.

**Releases list view:**

- Each row where `status === "Draft"` gains a **⋮ (kebab) menu** with a single option: **"Delete draft"**.
- Clicking it opens a confirmation dialog: `"Delete draft release '<name>'? This cannot be undone."` with **Cancel** and **Delete** (destructive red) buttons.
- On confirmation, call `DELETE /api/projects/{projectId}/releases/{id}`.
- On success: remove the row from the list with a fade-out animation and show a toast: `"Draft release '<name>' deleted."`.
- On `409 Conflict` (race condition — someone published it between the user clicking and confirming): show an error toast: `"This release has been published and can no longer be deleted."` and refresh the row status.

**Release detail page:**

- When the release is in `Draft` status and the user is Admin, show a **"Delete draft"** button in the page header (secondary, destructive style), adjacent to the existing "Edit" button.
- Same confirmation dialog and error handling as the list view.
- On successful deletion, navigate back to the project's Releases list.

**Acceptance criteria:**
1. Viewer role: no Delete option visible anywhere.
2. Admin, Published release: no Delete option visible.
3. Admin, Draft release: kebab menu on list row shows "Delete draft"; detail page shows "Delete draft" button.
4. Confirming deletion removes the release; cancelling does nothing.
5. `409` mid-flight is handled gracefully without a blank screen.

---

## `/plan` addendum

### Data model changes

**`Repository` entity** — add:
```
ServiceOwner  string?  max 120 chars  nullable
```
Migration: `AddColumn_Repositories_ServiceOwner` — add nullable column, no backfill needed.

**`ReleaseNoteTemplate` entity** — add:
```
IsSystem  bool  default false
```
Migration: `AddColumn_Templates_IsSystem` — add column; seed the "Release Summary (Default)" row with `IsSystem = true` and the Handlebars body from Feature C above.

**No other schema changes.** The refresh token cookie and setup key are runtime/environment concerns only.

---

### Service changes

**`ISetupService` / `AuthController`:**
- Read `RELEASE_MANAGER_SETUP_KEY` from `IConfiguration` at startup via an `IHostedService` validator that calls `IHostApplicationLifetime.StopApplication()` and logs fatal if the key is absent and no users exist.
- Add `SetupKeyAuthorizationFilter` (action filter, not middleware) applied only to the `setup` endpoint. Reads the `X-Setup-Key` header and short-circuits with `401` if invalid.
- Never bind the key value into any DTO or log parameter.

**`ITemplateService`:**
- `GetAllAsync` returns `isSystem` flag.
- `UpdateAsync` and `DeleteAsync` throw `ForbiddenException` when `IsSystem = true`.
- Add `CloneAsync(templateId)` — copies body, name (appends " (copy)"), sets `IsSystem = false`.

**`ITemplateRenderingService`:**
- Extend `ReleaseRenderContext` with `IReadOnlyList<RepoSummaryContext> Repositories` where `RepoSummaryContext` maps from `ReleaseRepository` join rows plus the parent `Repository.ServiceOwner`.
- `serviceOwner` must be coalesced to `""` before being passed into the Handlebars data model.

**`IProjectService`:**
- `CreateAsync` auto-creates a `ProjectTemplateBinding` for the system "Release Summary (Default)" template with `Kind = ReleaseSummary`, `SortOrder = 1`.

---

### API changes

| Method | Route | Change |
|---|---|---|
| `POST` | `/api/v1/auth/setup` | Add `SetupKeyAuthorizationFilter`; return `401` / `409` as specified |
| `GET` | `/api/v1/repositories/{id}` | Add `serviceOwner` to response |
| `PUT` | `/api/v1/repositories/{id}` | Accept and persist `serviceOwner` |
| `GET` | `/api/v1/templates` | Add `isSystem` to response |
| `PUT` | `/api/v1/templates/{id}` | Return `403` when `isSystem = true` |
| `DELETE` | `/api/v1/templates/{id}` | Return `403` when `isSystem = true` |
| `POST` | `/api/v1/templates/{id}/clone` | New endpoint — clone a system template |
| `POST` | `/api/v1/auth/refresh` | Set refresh token as `httpOnly` cookie (if not already) |

All existing endpoints accepting `RepositoryDto` implicitly gain `serviceOwner` — no versioning needed (additive field, non-breaking).

---

### Frontend changes

| Area | Change |
|---|---|
| Auth store (Zustand) | Add proactive refresh timer; expose `scheduleRefresh(exp)` |
| API client interceptor | Add 401-intercept → refresh → retry once logic with shared in-flight promise |
| `RepositoriesTable` | Add "Service Owner" column; inline edit field in row detail / edit panel |
| `TemplatesTable` | Add `[System]` badge; swap Edit/Delete for Clone button on system rows |
| `ReleasesTable` | Add kebab menu on Draft rows (Admin only); wire Delete with confirmation dialog |
| `ReleaseDetailPage` | Add "Delete draft" button in header for Draft + Admin; navigate away on success |

---

## `/tasks` — Milestone 13 definition

Append to `04-tasks-guidance.md`:

```
## Milestone 13 — Security, Service Ownership & UX Hardening

### A · Setup endpoint API key (backend only)
- Add startup validator (`IHostedService`) for `RELEASE_MANAGER_SETUP_KEY`.
- Add `SetupKeyAuthorizationFilter` and apply to the setup action.
- Unit tests: missing key → app refuses to start; wrong header → 401; correct header + empty DB → 201; correct header + existing user → 409.

### B · `service_owner` field
- EF Core migration: nullable `ServiceOwner` column on `Repositories`.
- Update `RepositoryDto`, `UpdateRepositoryRequest`, and the service layer.
- Frontend: add "Service Owner" input to the repo edit panel in Settings → Repositories.
- Unit test: field round-trips through API.

### C · Release Summary system template
- EF Core migration: `IsSystem` column on `ReleaseNoteTemplates`; seed row with the default Handlebars body.
- Extend `ReleaseRenderContext` with `Repositories` list including `serviceOwner`.
- Guard `UpdateAsync`/`DeleteAsync` with `ForbiddenException` when `IsSystem`.
- Add `POST /api/v1/templates/{id}/clone` endpoint.
- Auto-bind to new projects in `IProjectService.CreateAsync`.
- Frontend: `[System]` badge, Clone button, hide Edit/Delete for system templates.
- Integration test: preview render for a 2-repo release returns table with 2 rows and correct `serviceOwner` values.

### D · Frontend JWT refresh
- Switch refresh token to `httpOnly` cookie (backend `auth/refresh` sets `Set-Cookie`).
- Implement shared in-flight refresh promise in the API client.
- 401-intercept → refresh → retry once.
- Proactive refresh timer: decode `exp` from access token; schedule silent refresh at `exp - 2 min`.
- E2E test (Playwright or Vitest): expired token causes silent refresh, not redirect.

### E · Delete Draft releases UI
- `ReleasesTable`: kebab menu on Draft rows (Admin); confirmation dialog; optimistic row removal.
- `ReleaseDetailPage`: "Delete draft" button; navigate on success; handle 409 race.
- Unit test: Viewer sees no kebab; Published row has no kebab.

**Smoke test (Milestone 13):**
> 1. Deploy with `RELEASE_MANAGER_SETUP_KEY` unset → app refuses to start (check logs).
> 2. Set the key, restart, call `/auth/setup` with correct header → admin created.
> 3. Set `ServiceOwner = "Platform Team"` on a repo; create a release including that repo; preview the Release Summary template → table row shows "Platform Team".
> 4. Wait for JWT to near-expiry (or shorten JWT lifetime in dev config to 30 s); make an API call → succeeds silently; no redirect to login.
> 5. Admin creates a Draft release; clicks "Delete draft" from the list; confirms → release disappears with toast; navigating to its URL returns 404.
```

---

## Open clarifications

The following questions are pre-answered based on architectural decisions in the existing spec. If `/clarify` surfaces them, use these answers:

| Question | Answer |
|---|---|
| Should the setup key be rotatable after setup? | No. Once setup is complete the endpoint is disabled; the key is irrelevant thereafter. |
| Should `ServiceOwner` be a free-text field or a lookup to a users table? | Free text in v1. A lookup can be added in v2 if user management grows. |
| Should the system template be editable in place by super-admins? | No. Clone-then-edit is the only path. Keeps the seed row stable for integration tests. |
| Should the release summary template be the default for existing projects? | No. Only new projects get it auto-bound. Existing projects can add it manually via Settings → Projects → Template Bindings to avoid surprise changes to live release workflows. |
| Should the refresh token cookie domain be locked? | Yes — `SameSite=Strict`, `Secure` (HTTPS only), `Domain` left to the default (same origin). Document in the deployment README. |
| What happens if a Draft release is deleted while another user has the detail page open? | The next API call they make returns `404`. The detail page should handle `404` gracefully by showing a "Release not found" message and a link back to the project. |
