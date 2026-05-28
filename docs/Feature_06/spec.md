# Feature Specification: Project Page Templates

**Feature Branch**: `012-project-page-templates`
**Created**: 2026-05-24
**Status**: Draft
**Input**: User description: "Enhance Confluence page in the release process — use Handlebars and predefined values (repo name, next tag, valuable info) and define this template per project; when a release starts it fills all information and creates the release page based on these templates."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Bind multiple auto-filled page templates to a project (Priority: P1)

A tech lead opens their project's settings and binds two Handlebars templates to it — a release-notes template and a smoke-test checklist template — each with its own page-title format and parent-page target. When they later start a release for that project, the system auto-fills variables (project name, next version, previous version, per-repo previous and next tags, ticket buckets, contributors) and renders both pages, ready to preview and publish.

**Why this priority**: This is the core capability the user asked for. Without it the platform still produces a single release-notes page with limited variables; with it, every project can publish a coordinated set of pages reflecting its own release process. Every other story in this feature depends on this binding model existing.

**Independent Test**: An admin can bind two templates to a project, run the release wizard, and end up with two published Confluence pages with auto-filled values, without touching any other story.

**Acceptance Scenarios**:

1. **Given** a project named "Payments" with a "version primary" repository on tag `1.30.0`, **When** the admin binds a release-notes template with title format `"{{project.name}} {{version}} — Release Notes"` and a checklist template with title format `"{{project.name}} {{version}} — Smoke Tests"` and runs the release wizard, **Then** two Confluence pages are created with titles `"Payments 1.31.0 — Release Notes"` and `"Payments 1.31.0 — Smoke Tests"`, with the next version computed as a minor bump.
2. **Given** a project with three repositories assigned, **When** the admin opens the release wizard, **Then** the prepared release-notes page body contains a table listing each repository with its previous tag, next tag, commit count, and ticket count populated from the latest Git data.
3. **Given** the admin has not configured custom variables on the project, **When** a template references `{{custom.slackChannel}}`, **Then** the rendered output shows an empty string for that token and the wizard surfaces a non-blocking warning naming the undefined variable.

---

### User Story 2 - Edit, reorder, and cross-link the prepared pages before publishing (Priority: P1)

When the release wizard opens, the tech lead sees one editable tab per bound template, each with an editable title and body. They can adjust copy, reorder publish sequence, and toggle whether each page should appear as a related link on the primary release-notes page. On publish, pages are created in the chosen order and cross-linked automatically.

**Why this priority**: Auto-fill is necessary but not sufficient — real releases always need some last-mile manual edits, and the cross-linking is what makes a multi-page release feel like one cohesive artefact rather than orphaned siblings.

**Independent Test**: Given a project with two bound templates (one with `LinkFromReleaseNotes = true`), the admin can edit either page's body in the wizard, publish, then open Confluence and find the linked page referenced from the primary release-notes page.

**Acceptance Scenarios**:

1. **Given** two prepared pages in the wizard, **When** the admin edits the checklist page's body, navigates to the preview step, and then back to edit, **Then** their edits persist within the wizard session.
2. **Given** a checklist binding with `LinkFromReleaseNotes = true` and sort order 2, **When** the release is published, **Then** the release-notes page (sort order 1) is updated to include a related-pages section linking to the checklist page.
3. **Given** an admin renames a prepared page's title to one that already exists in the same parent page in Confluence, **When** they attempt to publish, **Then** the system updates the existing page rather than creating a duplicate.

---

### User Story 3 - Refresh prepared pages with Jira reconciliation data (Priority: P2)

After the tech lead optionally runs Jira reconciliation in the wizard, they can refresh the prepared pages so any template using the `{{reconciliation}}` variable now shows the matched/Jira-only/Git-only counts and match rate.

**Why this priority**: Reconciliation already exists in the platform and is one of its strongest features. Surfacing its result directly on the release notes makes the published page far more useful for stakeholders who can't be expected to read the reconciliation view separately. Lower priority than P1 because the feature is shippable without it — templates can omit the reconciliation block.

**Independent Test**: Given a template with a `{{#if reconciliation}}` block, when the admin runs reconciliation then clicks "Refresh pages with reconciliation data", the rendered page body now includes the reconciliation summary panel; without that click it remains absent.

**Acceptance Scenarios**:

1. **Given** a wizard where reconciliation has not been run, **When** the prepared pages are rendered, **Then** template blocks guarded by `{{#if reconciliation}}` are omitted from the output.
2. **Given** a wizard where reconciliation has just been run, **When** the admin clicks "Refresh pages with reconciliation data", **Then** every prepared page is re-rendered with the reconciliation context populated and the user's prior manual edits to that page are preserved as a draft they can choose to discard or keep.

---

### User Story 4 - Backward-compatible migration of existing single-template projects (Priority: P2)

When the application is upgraded to include this feature, every existing project that already had a default release-notes template assigned continues to produce the same release-notes output it produced before, without any admin action.

**Why this priority**: This isn't a user-facing capability so much as a deployment safety property, but it is essential — without it, this feature would break every existing project on upgrade. Lower priority than P1 only because it sits behind a migration script and isn't a daily user workflow.

**Independent Test**: Take a snapshot of a database with three projects each holding a different `DefaultReleaseNoteTemplateId`, run the migration, then run the release wizard for each project and confirm that the rendered release-notes page matches what would have been produced before the upgrade.

**Acceptance Scenarios**:

1. **Given** an existing project with `DefaultReleaseNoteTemplateId` set to template T, **When** the data migration runs, **Then** the project gains exactly one `ProjectTemplateBinding` of kind `ReleaseNotes` referencing template T, with sort order 0 and a default page-title format.
2. **Given** a project that had no `DefaultReleaseNoteTemplateId` before the upgrade, **When** the migration runs, **Then** no binding is created and the project settings UI prompts the admin to bind a release-notes template before they can run the wizard.

---

### Edge Cases

- What happens when the version-primary repository has no semver tags yet? The system surfaces a validation error in the "Prepare pages" step naming the repository and offers an admin override that lets them type the version explicitly.
- How does the system handle a template that references a variable that doesn't exist (typo, e.g. `{{custom.slakChannel}}`)? The render does not fail; the unknown token renders as an empty string, and the wizard preview surfaces a non-blocking warning listing every unknown token encountered so the admin can correct templates without aborting the release.
- What happens when the rendered page title exceeds Confluence's 255-character limit, or renders to an empty string? The wizard's preview step blocks the publish action and shows the offending page tab with an inline error.
- What happens when a project has zero bindings, or has no `ReleaseNotes` binding? The wizard refuses to open and redirects the admin to Settings → Projects → Pages with a banner explaining a release-notes binding is required.
- What happens when two bindings render to the same page title? The first published page is created; the second triggers an update of that same page (idempotent by title under parent), which is almost certainly not what the author wanted. The wizard surfaces a warning when prepared page titles collide, before publish.
- What happens when reconciliation has been run but then the admin changes the change-range and reconciliation becomes stale? The wizard marks the reconciliation badge as "stale" and the "Refresh pages with reconciliation data" button is disabled until reconciliation is re-run.
- What happens to manually edited page bodies when the admin re-renders (e.g. after running reconciliation)? The system keeps the edited body as a draft and prompts per-tab: "keep my edits" or "discard and use freshly rendered output".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow administrators to bind one or more release-note templates to a logical project, with each binding declaring a kind, page-title template, parent-page override (optional), `LinkFromReleaseNotes` flag, and sort order.
- **FR-002**: System MUST enforce that every project participating in releases has exactly one binding of kind `ReleaseNotes`, and MUST prevent deletion of the last `ReleaseNotes` binding.
- **FR-003**: System MUST support per-project custom string key/value variables, surfaced to templates under the `{{custom.<key>}}` namespace.
- **FR-004**: System MUST resolve the next release version per project from the version-primary repository's latest semver tag, using a per-project bump strategy of `Patch`, `Minor` (default), or `Major`.
- **FR-005**: System MUST compute a per-repository Jira fix-version name following the existing platform convention `<RepoName>_<NextVersion>` (consistent with the per-repo Jira tickets feature).
- **FR-006**: System MUST render Handlebars templates server-side with access to `project`, `version`, `previousVersion`, `releaseDate`, `repositories`, `tickets` (bucketed Breaking/Features/Fixes/Other), `contributors`, `reconciliation` (nullable), `confluence`, and `custom` variables.
- **FR-007**: System MUST provide built-in Handlebars helpers `formatDate`, `length`, `eq`, `gt`, `minus`, `lower`, `upper`, `truncate`, and `jiraLink`.
- **FR-008**: System MUST render unknown variable references as empty strings without failing the render, and MUST surface the set of unknown tokens in the wizard preview as a non-blocking warning.
- **FR-009**: Release wizard MUST begin with a "Prepare pages" step that renders all bound templates with auto-resolved context and presents one editable tab per page.
- **FR-010**: Release wizard MUST allow per-page editing of title and body; edits MUST persist across wizard step navigation within the same session.
- **FR-011**: Release wizard MUST validate that every rendered page title is non-empty and at most 255 characters before allowing publish.
- **FR-012**: System MUST detect prepared-page title collisions before publish and surface them as warnings in the preview step.
- **FR-013**: System MUST publish prepared pages in `SortOrder`, treating the `ReleaseNotes` page as primary and appending a related-pages section to it that links to every other prepared page whose binding has `LinkFromReleaseNotes = true`.
- **FR-014**: Publishing MUST be idempotent: re-running publish for the same release updates existing Confluence pages matched by space, parent page, and title rather than creating duplicates.
- **FR-015**: System MUST allow administrators to re-render prepared pages after running Jira reconciliation, preserving manual edits as a draft the admin can keep or discard per page.
- **FR-016**: System MUST mark reconciliation as "stale" if the release change range is modified after reconciliation was run, and MUST disable the "Refresh pages with reconciliation data" action until reconciliation is re-run.
- **FR-017**: On upgrade, system MUST migrate every project with a `DefaultReleaseNoteTemplateId` to a single `ProjectTemplateBinding` of kind `ReleaseNotes`, sort order 0, using a default page-title template, preserving prior release-notes output.
- **FR-018**: System MUST refuse to open the release wizard for any project that has zero bindings or lacks a `ReleaseNotes` binding, redirecting the administrator to the Pages settings.
- **FR-019**: Audit log MUST record every create, update, delete, and reorder of a project template binding with the actor, project, kind, and template ID.
- **FR-020**: Viewer role MUST be able to read bindings and custom variables; Admin role MUST be required for any create, update, delete, or reorder.
- **FR-021**: Custom variable values MUST be stored as plain text and MUST NOT be used to store secrets; system documentation MUST clearly state this.
- **FR-022**: System MUST surface a validation error in the "Prepare pages" step if the version-primary repository has no semver tag, allowing an admin override that accepts an explicitly-typed version string.
- **FR-023**: System MUST allow administrators to reorder bindings via the Pages settings UI; reorder operations MUST be reflected immediately in subsequent wizard sessions.
- **FR-024**: Templates pane in Settings → Templates MUST gain a sample-context selector permitting either "Synthetic sample" or "Latest release of <project>" as the live-preview context source.

### Key Entities

- **ProjectTemplateBinding**: A directed association between a logical project and a release-note template that declares how that template should be used for releases. Key attributes: kind (`ReleaseNotes`, `Checklist`, `Custom`), page-title template (Handlebars string), parent-page override (optional), `LinkFromReleaseNotes` flag, sort order.
- **ProjectCustomVariable**: A scoped key/value pair attached to a project, exposed in templates as `{{custom.<key>}}`. Strings only; not secrets.
- **VersionBumpStrategy**: A per-project setting choosing how the next version is computed from the primary repo's tag. Values: `Patch`, `Minor`, `Major`.
- **ReleaseRenderContext**: The composite read-only value that feeds every Handlebars render. Contains project metadata, resolved version and previous version, release date, the list of repositories with their per-repo previous and next tags and Jira fix-version names, ticket buckets, contributors, optional reconciliation summary, Confluence target, and the custom-variables map.
- **PreparedPage**: An in-wizard, pre-publish representation of one rendered page. Carries kind, rendered title, rendered body, parent page id, `LinkFromReleaseNotes` flag, sort order, and a dirty-edit flag.
- **PreparedRelease**: The wizard-session aggregate of the render context plus the ordered list of prepared pages, used by the API as the response to the preview endpoint.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can bind two templates to a project, configure custom variables, run the wizard, and publish two cross-linked Confluence pages in under 5 minutes from a cold start, including manual edits.
- **SC-002**: The "Prepare pages" step renders all bound templates with auto-resolved context in under 2 seconds for a typical project with 3 repositories, 30 commits, and 15 tickets.
- **SC-003**: Re-publishing the same release within one hour produces zero duplicate pages in Confluence and zero broken cross-links.
- **SC-004**: 100% of projects that had a `DefaultReleaseNoteTemplateId` before upgrade produce a byte-equivalent release-notes title and an output that the same template would have produced before the upgrade, with no administrator action.
- **SC-005**: Across a sample of 5 tech leads, at least 4 publish a multi-page release without consulting written documentation on their first attempt.
- **SC-006**: Zero releases reach Confluence with a page-title exceeding 255 characters or rendering to the empty string.

## Assumptions

- Confluence Cloud target environment supports the existing storage-format publishing already implemented in Milestone 8; no new macros or endpoints are required for cross-linking beyond the existing `CreateOrUpdatePageAsync`.
- Existing Handlebars rendering library used elsewhere in the application can be reused for both page bodies and page titles.
- The per-repo Jira fix-version naming convention `<RepoName>_<NextVersion>` is already established by the existing per-repo Jira tickets feature and is treated here as a stable input, not redefined.
- Custom variables are intentionally plain text and not encrypted; secrets continue to flow through the existing encrypted token-storage path for integrations.
- The wizard session is bounded to one user and one browser tab; no concurrent multi-user editing of prepared pages is supported in this feature.
- The audit log infrastructure from earlier milestones is available and will accept new event types without schema changes.
