# Tasks — Per-Repo Release Versioning

> Generate tasks for the R1–R5 milestones below. Each task must include the smoke test as an acceptance criterion. Reuse existing services, DTOs, and components where the plan addendum says so.

Five milestones, each individually shippable. The order is dependency-driven — earlier milestones can be merged and deployed before later ones land.

---

## Milestone R1 — Data Model & Migration

**Goal:** `ReleaseRepository` entity exists in the database, populated for all existing releases.

**Tasks:**

1. Add `ReleaseRepository` entity to `RepoManager.Domain` matching the schema in the plan addendum.
2. Add `DbSet<ReleaseRepository> ReleaseRepositories` to the DbContext.
3. Add EF Core configuration for `ReleaseRepository`: unique index `(ReleaseId, RepositoryId)`, single-column index on `RepositoryId`, cascade from `Release`, restrict from `Repository`.
4. Add navigation collection `Release.ReleaseRepositories` and configure it as a one-to-many.
5. Generate the EF migration `AddReleaseRepository`.
6. Append a raw-SQL backfill statement to the migration's `Up()` method that inserts one row per existing `Release`, pointing at the project's primary repo, with `NextVersion = Release.Version`, `BumpType = "manual"`, and empty snapshot fields.
7. Add a `Down()` rollback that drops the table without touching `Release`.

**Smoke test:**

> Apply the migration against a development database that has at least one existing `Release` with `Version = "2.5.0"` and a project with a primary repo. After migration: `SELECT * FROM ReleaseRepositories WHERE ReleaseId = <existing>` returns exactly one row with `NextVersion = "2.5.0"`, `BumpType = "manual"`, `RepositoryId` equal to the project's `PrimaryRepositoryId`.

---

## Milestone R2 — Preview Endpoint

**Goal:** The wizard can fetch per-repo version suggestions without persisting anything.

**Tasks:**

1. Extract the existing version-bump logic into `IVersionBumpService` if it isn't already an injectable service. The interface signature matches the plan addendum exactly.
2. Add unit tests for `IVersionBumpService.SuggestAsync` covering: no-commits, only-fixes (→ patch), feat-included (→ minor), `BREAKING CHANGE` footer (→ major), `feat!:` marker (→ major).
3. Create `IReleaseCompositionService` and `ReleaseCompositionService` in `RepoManager.Application`.
4. Implement `PreviewAsync` — iterates given `repositoryIds`, calls `IVersionBumpService.SuggestAsync` per repo, computes `DerivedReleaseVersion` and `DerivedFromRepositoryId` using the rule in the plan addendum.
5. Add `POST /api/projects/{projectId}/releases/preview` endpoint, Admin-only, wiring to the new service.
6. Add a validator that rejects unknown repository IDs (i.e. not in `ProjectRepository`) with `400 ValidationProblem`.

**Smoke test:**

> Given a project with primary repo `apply-api` (latest tag `2.5.0`, with `feat:` commits since) and secondary repo `apply-web` (latest tag `1.12.3`, with only `fix:` commits since), call the preview endpoint with both repo IDs. Response includes `derivedReleaseVersion: "2.6.0"`, `derivedFromRepositoryId` equal to the primary, `apply-api` row showing `suggestedNextVersion: "2.6.0"` with `bumpType: "minor"`, and `apply-web` row showing `suggestedNextVersion: "1.12.4"` with `bumpType: "patch"`.

---

## Milestone R3 — Create / Read / Update / Delete

**Goal:** Releases can be created, fetched, edited (while Draft), and deleted via API.

**Tasks:**

1. Implement `IReleaseCompositionService.CreateDraftAsync` — runs validations from the plan addendum, captures snapshot fields server-side via `IVersionBumpService`, derives `Release.Version`, persists in a single transaction.
2. Implement `IReleaseCompositionService.UpdateDraftAsync` — throws `ConflictException` if status is not `Draft`; otherwise wholesale-replaces the `ReleaseRepository` collection.
3. Add `POST /api/projects/{projectId}/releases` (Admin), returning `201 Created`.
4. Add `GET /api/projects/{projectId}/releases/{id}` (Viewer) returning the release with `ReleaseRepositories` eagerly loaded.
5. Add `PUT /api/projects/{projectId}/releases/{id}` (Admin) — returns `409 Conflict` code `release_not_draft` when status forbids edits.
6. Add `DELETE /api/projects/{projectId}/releases/{id}` (Admin) — Draft only; Published/Archived deletes return `409 Conflict`.
7. Add integration test (Testcontainers) covering full create → fetch → update → delete on a Draft release.
8. Add integration test verifying that a client-supplied `previousVersion` in the create request is **ignored** (server overwrites with freshly-fetched value).

**Smoke test:**

> `POST /api/projects/1/releases` with a Name and two repos (each with a `nextVersion` strictly greater than the current tag) returns `201` with a body containing two `ReleaseRepositories` rows. `GET /api/projects/1/releases/{newId}` returns the same data with `version` equal to the primary repo's `nextVersion`. `PUT` removing one of the repos and re-fetching shows a single `ReleaseRepositories` row and a recomputed `version`.

---

## Milestone R4 — Wizard Step

**Goal:** The release-creation wizard has a working "Repos & versions" step matching the spec mockup.

**Tasks:**

1. Add `<ReleaseRepoSelectionStep />` in `frontend/src/features/releases/wizard/`.
2. On mount, call `POST /releases/preview` with all project repo IDs to seed initial state. Pre-check repos where `hasChanges === true`.
3. Render rows per the spec: checkbox, repo name, primary star, `previousVersion → nextVersion` input, bump-type radios, change counts.
4. Wire the bump-type radios to recompute the next-version input from the previous version. Wire manual edits to the next-version input to flip the radio to `custom`.
5. Add a Zod schema validating each selected row: `nextVersion` is valid semver and strictly greater than `previousVersion`. Disable "Next" until at least one row is selected and all selected rows are valid.
6. Render the derived release label in the footer: `"This release will be labeled X.Y.Z (from <repoName>, primary)"` or `"... (from <repoName>, fallback)"` when primary is excluded.
7. On wizard submission, call `POST /releases` with the selection set and navigate to the release detail page.
8. Feature-flag the new step behind a config toggle (`features.perRepoVersioning`) so it can be turned off if regressions appear post-merge.

**Smoke test:**

> Open the release wizard for a project with three repos (one primary with changes, one secondary with changes, one with no changes). The first two are pre-checked. The footer shows the primary repo's suggested version as the release label. Editing the primary repo's next-version field updates the footer live. Submitting creates a release; the detail page shows two `ReleaseRepository` rows.

---

## Milestone R5 — Releases List & Detail Update

**Goal:** The Project page shows all releases; the release detail page renders the per-repo table.

**Tasks:**

1. Add `<ProjectReleasesList projectId>` component using TanStack Query and shadcn `<Table />`.
2. Wire columns: Name, Version, Date created, Status, Repo count. Clicking a row navigates to the release detail page.
3. Add status filter (shadcn `<Select />`) and debounced name search (`<Input />`).
4. Add the component as a new tab on the project detail page (e.g., between "Overview" and "Settings").
5. Add `<ReleaseRepositoriesTable />` on the release detail page rendering: repo name, previous → next, bump type badge, commit count, ticket count.
6. Handle the legacy-release rendering case: when snapshot fields are empty, show em-dashes and a "Pre-feature release — partial data" badge above the table.
7. Verify accessibility: keyboard navigation through the table rows, focus styles on row hover, screen-reader labels on the bump-type badges.

**Smoke test:**

> Navigate to a project that has at least three releases (a mix of Draft, Published, and a legacy backfilled one). The Releases tab lists all three with correct columns. Filter by Status = Published — the legacy release appears (it was backfilled as Published) and the Draft is hidden. Click into the legacy release — the per-repo table shows the primary repo row with em-dashes in the previous-version, from-sha, to-sha columns and the partial-data badge above.

---

## Notes for `/analyze`

- Flag the Jira reconciliation impact (per the plan addendum): the reconciliation view currently iterates over `Project.ProjectRepositories` and should later iterate over `Release.ReleaseRepositories`. **This is a follow-up feature, not part of this branch.**
- Flag the three Open Questions from the spec addendum: primary-repo-excluded fallback behaviour, legacy backfill acceptance, reconciliation deferral.
