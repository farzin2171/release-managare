# Research: Project Page Templates

**Phase**: 0 — Outline & Research
**Branch**: `007-project-page-templates`
**Date**: 2026-05-24

## Summary

Seven design decisions govern the implementation of multi-template page rendering, the wizard session model, and the backward-compatible migration. All decisions are resolved; no open questions remain.

---

## Decision 1: Unknown Handlebars token detection via IFormatterProvider

**Decision**: Register a `MissingTokenRecorder` that implements `IFormatterProvider` on the `IHandlebars` instance. The recorder intercepts `UndefinedBindingResult` values — the internal type HandlebarsDotNet writes when a path segment resolves to undefined — and captures the token name into a `[ThreadStatic]` `HashSet<string>` without writing any output (resulting in empty string). Each render invocation calls `BeginCapture()` before rendering title + body on the same thread, then `EndCapture()` to retrieve the deduplicated unknown-token set.

For `{{custom.<key>}}` tokens, the `ProjectCustomVariables` dictionary wrapper overrides its indexer to call `MissingTokenRecorder.Record($"custom.{key}")` directly for missing keys, enabling fully qualified names in warnings.

**Rationale**: Single-pass (no double-parse), zero render-output impact, uses the exact extension point the library provides, fully isolated per render via `[ThreadStatic]`, no reflection at call time (type lookup done once at startup). `ThrowOnUnresolvedBindingExpression` defaults to `false`, so empty-string fallback requires no configuration change.

**Alternatives considered**:
- `ThrowOnUnresolvedBindingExpression + try/catch loop`: stops at the first unknown token; requires re-render per token to collect all. Rejected.
- Pre-render AST scan via `Handlebars.Parse`: internal API, reflection-heavy, cannot distinguish helpers from bindings without resolving. Rejected.
- `IHelperResolver`: fires only for `{{helper args}}` invocations, not plain binding expressions. Produces zero entries for `{{custom.slakChannel}}`. Rejected.
- Post-render text scan for empty spans: false positives on genuinely empty values. Rejected.

---

## Decision 2: Wizard session state in Zustand with sessionStorage persistence

**Decision**: All prepared page edits live in a dedicated `useWizardStore` Zustand store persisted to `sessionStorage` (survives tab refresh, cleared on tab close — matching the spec's single-tab wizard lifecycle). Each page slot uses a `DraftState` discriminated union:

```
DraftState =
  | { kind: 'server' }                                              // unedited
  | { kind: 'edited'; title: string; body: string }                // user-edited
  | { kind: 'conflict'; serverTitle: string; serverBody: string;   // re-render arrived
      draftTitle: string; draftBody: string }
```

The `reRenderPages` store action merges freshly rendered pages into the existing slots: `server → server`, `edited → conflict`, `conflict → conflict` (preserves prior draft, updates server side). `resolveConflict('keep')` → `edited`; `resolveConflict('discard')` → `server`. Publish is blocked until all `conflict` slots are resolved.

**Rationale**: TanStack Query optimistic updates model the server as source of truth and roll back on error — wrong semantics for drafts that exist before any server write. React `useState` in the wizard component tree is lost on step navigation because steps render conditionally. Server-side draft rows add a cleanup lifecycle and a per-keystroke round-trip. The `DraftState` union makes three states explicit and exhaustive; flat `isDirty: boolean` + `hasFreshRender: boolean` flags produce one invalid combination and require multi-field conflict checks.

**Alternatives considered**:
- TanStack Query optimistic updates: wrong for "draft before server write." Rejected.
- React `useState` in wizard component tree: lost on step unmount. Rejected.
- `dirty + draft` flat flags: allows invalid state, complicates conflict detection. Rejected.
- Server-side draft rows in SQLite: over-engineered for single-tab session. Rejected.

---

## Decision 3: VersionBumpStrategy resolves project-level template version

**Decision**: `Project.VersionBumpStrategy` (Patch | Minor | Major, default Minor) is a new project-level setting used exclusively by `ReleaseRenderService` when constructing the `{{version}}` and `{{previousVersion}}` template variables. If the release already has a `ReleaseRepository` row for the version-primary repository (from Feature 005), its `NextVersion` IS the template `{{version}}`. Otherwise, the service fetches the primary repo's latest semver tag from the database and applies the bump strategy.

`IVersionBumpService.SuggestAsync` is NOT modified — it continues to use conventional commit analysis for per-repo versioning. `VersionBumpStrategy` is a project-level override that only affects the render context computation.

**Rationale**: The Feature 005 per-repo snapshot (`ReleaseRepository.NextVersion`) is the authoritative version if available. `VersionBumpStrategy` serves as the fallback for projects not yet using Feature 005 (e.g., legacy releases, projects with no ReleaseRepository rows). This keeps the two features composable without coupling them.

**Alternatives considered**:
- Modify `IVersionBumpService.SuggestAsync` to accept `VersionBumpStrategy`: creates a hard coupling between Feature 005 and 007 service logic. Rejected.
- Always recompute from tags, ignoring Feature 005 snapshot: stale if tags have moved since the release was created. Rejected.

---

## Decision 4: PreparedRelease is stateless on the server; client owns wizard state

**Decision**: `POST /api/v1/releases/{id}/prepare-pages` renders all bound templates server-side and returns a `PreparedReleaseDto` (render context + array of `PreparedPageDto`). This endpoint is stateless — it does not persist the render result. The React client stores the returned pages in the Zustand wizard store. When the user publishes, `POST /api/v1/releases/{id}/publish-pages` accepts the final (potentially edited) `PreparedPageDto[]` from the client and publishes them to Confluence.

**Rationale**: Server-side session state requires a cleanup lifecycle and complicates horizontal scaling. The spec explicitly bounds wizard sessions to one user, one tab — no concurrent editing to mediate. Keeping the server stateless simplifies the API and puts edit ownership clearly in the client.

**Alternatives considered**:
- Store `PreparedRelease` in SQLite for the duration of the wizard: adds a cleanup job, complicates migration, provides no value for the single-tab use case. Rejected.
- Pass reconciliation data directly to `publish-pages` endpoint without re-render: the client already has the rendered bodies; re-rendering at publish time adds latency and conflicts with user edits. Rejected.

---

## Decision 5: Cross-linking via post-publish Confluence page update

**Decision**: After all prepared pages are published in `SortOrder` and their Confluence page IDs are known, `ConfluencePublisher` updates the primary (`ReleaseNotes`) page by appending a "Related pages" section containing links to every page whose binding has `LinkFromReleaseNotes = true`. The links use Confluence storage-format `<ri:page>` macros referencing the published page IDs.

The existing `IConfluencePublisher.CreateOrUpdatePageAsync` (Milestone 8) handles both create and idempotent update. The cross-link section is appended to the raw storage-format body before the final update call for the primary page.

**Rationale**: The existing publish infrastructure already supports idempotent create/update. Adding a cross-link step only requires one additional `UpdatePageAsync` call after all pages are published. No new Confluence API endpoints are needed (spec Assumption 1).

**Alternatives considered**:
- Embed cross-links as template variables: requires the template author to know page IDs before publishing (impossible). Rejected.
- Use Confluence's native "Related pages" macro: not available in Cloud REST v2 storage format without specific macro IDs. Rejected.
- Inline links in the template using `{{confluence.linkedPage.url}}`: page URL not available until after publish. Rejected.

---

## Decision 6: Reconciliation staleness is client-side state

**Decision**: The Zustand wizard store tracks `reconciliationState: { ran: boolean; stale: boolean; data: ReconciliationSummaryDto | null }`. When the user modifies the change-range after reconciliation is run, the wizard dispatches `markReconciliationStale()`. The "Refresh pages with reconciliation data" button is disabled when `stale === true` or `ran === false`. The server has no concept of stale reconciliation — it always accepts and applies the reconciliation payload sent with `POST /api/v1/releases/{id}/prepare-pages`.

**Rationale**: Staleness is a wizard-session concern, not a durable server concern. The server cannot know that the change-range was modified in the same client session. Client-side detection is simpler and sufficient for the single-tab use case.

**Alternatives considered**:
- Store a `ReconciliationInputHash` in the Release row server-side: adds a database column and a server-round-trip for an ephemeral wizard-session concern. Rejected.
- Always re-run reconciliation before rendering: forces users to wait for Jira API calls every time they navigate to the prepare step. Rejected.

---

## Decision 7: Migration drops DefaultReleaseNoteTemplateId in the same EF migration

**Decision**: The EF Core migration `AddProjectTemplateBindings` (a) adds `ProjectTemplateBinding` and `ProjectCustomVariable` tables, (b) adds `VersionBumpStrategy` column to `Project`, (c) inserts one `ProjectTemplateBinding` row of kind `ReleaseNotes` for every project where `DefaultReleaseNoteTemplateId IS NOT NULL` (sort order 0, page-title template `"{{project.name}} {{version}} — Release Notes"`), and (d) drops the `DefaultReleaseNoteTemplateId` column from `Project`. Steps (c) and (d) run in the same transaction as the schema changes.

**Rationale**: Keeping `DefaultReleaseNoteTemplateId` after the migration would leave a zombie column that could confuse future developers and cause divergence between the old and new code paths. Dropping it in the same migration ensures the schema and application code are in sync from the first deployment. The migration is idempotent because EF Core tracks applied migrations.

**Alternatives considered**:
- Keep `DefaultReleaseNoteTemplateId` and read both old and new columns: dual-write complexity, risk of divergence. Rejected.
- Separate migration for data backfill vs schema drop: more migrations, no benefit for a one-time upgrade. Rejected.

---

## NEEDS CLARIFICATION: None

All decisions resolved. No open questions remain.
