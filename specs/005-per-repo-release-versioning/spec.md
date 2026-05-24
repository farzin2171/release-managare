# Feature Specification: Per-Repo Release Versioning

**Feature Branch**: `006-per-repo-release-versioning`  
**Created**: 2026-05-23  
**Status**: Draft  
**Input**: User description: "@docs/Feature_05/01-specify-addendum.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Multi-Repo Release Composition (Priority: P1)

A tech lead creating a release needs to include only the repositories that have changes in the current cycle and set each repo's next version independently, so the release accurately reflects what is actually shipping.

**Why this priority**: This is the core value of the feature. Without it, teams cannot express per-repo versioning in a single release.

**Independent Test**: Can be fully tested by opening the release creation wizard with a project that has multiple assigned repos, selecting a subset, confirming per-repo versions, and verifying the release is saved with individual repo version snapshots.

**Acceptance Scenarios**:

1. **Given** a project has 3 assigned repos, **When** the admin opens the release wizard, **Then** repos with commits since their last tag are pre-selected and repos without changes are shown but unchecked.
2. **Given** the admin selects 2 of 3 repos and sets distinct next versions, **When** they submit, **Then** the release is saved with a per-repo version record for each selected repo, capturing the previous version, next version, bump type, and change counts.
3. **Given** the admin selects zero repos, **When** they attempt to submit, **Then** submission is blocked with a clear validation message requiring at least one repo.

---

### User Story 2 — Automatic Version Suggestion per Repo (Priority: P2)

A tech lead setting per-repo versions needs the system to pre-fill a suggested next version and bump type based on the commits in each repo's change range, so they don't have to manually calculate versions.

**Why this priority**: Manual version calculation is error-prone and time-consuming; suggestions reduce cognitive load and mistakes.

**Independent Test**: Can be fully tested by loading the release wizard and verifying that each repo shows a pre-filled next version computed from its latest semver tag and the dominant change type (breaking / feat / fix) in its commit range.

**Acceptance Scenarios**:

1. **Given** a repo's commit range contains a commit with a breaking change marker, **When** the wizard loads, **Then** the suggested bump type is `major` and the next version increments the major segment.
2. **Given** a repo's commit range has only `feat` commits, **When** the wizard loads, **Then** the suggested bump type is `minor`.
3. **Given** the admin changes the bump type radio to `patch`, **When** the change is applied, **Then** the next-version field updates automatically to the patch increment of the previous tag.
4. **Given** the admin manually types a next version, **When** the field is edited, **Then** the bump type radio switches to `custom` and validation ensures the entered version is a valid semver string greater than the previous version.

---

### User Story 3 — Releases List on Project Page (Priority: P3)

A tech lead viewing a project needs to see all releases ever created for it (name, version label, date, repo count, status), so they can audit release history without navigating away from the project.

**Why this priority**: Discoverability and historical audit are important but the feature works without this view; it enhances usability rather than enabling the core capability.

**Independent Test**: Can be fully tested by navigating to a project page, finding the Releases section, and verifying it lists all releases for that project with correct metadata and links to individual release detail pages.

**Acceptance Scenarios**:

1. **Given** a project has 5 releases in various statuses, **When** the admin views the project page, **Then** the Releases section shows all 5 releases sorted by most recently created first.
2. **Given** the Releases section is visible, **When** the admin filters by status `Published`, **Then** only published releases are shown.
3. **Given** the Releases section is visible, **When** the admin clicks a release row, **Then** navigation goes to the release detail page for that release.

---

### User Story 4 — Historical Snapshot on Release Detail (Priority: P2)

An admin reviewing a published release needs to see each included repo's previous version, new version, bump type, and change counts as they were at the moment of release creation, so the record stays accurate even if repo tags or branches change later.

**Why this priority**: Release records are audit artifacts; correctness at review time depends on immutable snapshotting, not live re-derivation.

**Independent Test**: Can be fully tested by viewing a published release's detail page and confirming it shows a per-repo table with the snapshot values, and that the values are unchanged if the underlying repo's latest tag is subsequently deleted.

**Acceptance Scenarios**:

1. **Given** a release was created with 3 repos at specific versions, **When** an admin views the release detail page, **Then** a per-repo table shows each repo's previous version → next version, bump type badge, commit count, and ticket count.
2. **Given** a legacy release created before this feature, **When** an admin views its detail page, **Then** the per-repo table shows the single primary repo row with empty snapshot fields indicated clearly (e.g., em-dashes), and a badge noting it is a pre-feature release.

---

### Edge Cases

- What happens when the project has no assigned repositories? The wizard should block release creation with a clear message to add repos first.
- What happens when a repo has no semver tag? The system shows the repo with a "no tag" indicator and disables version suggestion; the user must enter a version manually (starting from `0.1.0` is a reasonable default hint).
- What happens if the primary repo is not included in the release? The release label falls back to the alphabetically-first included repo's next version; the UI shows which repo the label is derived from.
- What happens when two admins try to publish the same Draft release simultaneously? The second publish operation should encounter a conflict and return a clear error.
- What happens when two admins try to edit the same Draft release simultaneously? A pessimistic lock is acquired when an admin opens the Draft for editing, with a TTL (suggested: 10 minutes, refreshed on activity). A second admin attempting to open the same Draft for editing is shown a "currently being edited by [name]" message and blocked until the lock expires or is released.
- What happens when the user edits a Published release's repo selection? The edit is blocked; only `NotesMarkdown` and `ConfluencePageUrl` remain editable after publish.
- What happens when an admin tries to remove a repository from the project while it is included in a Draft release? The unassignment is blocked; the admin must first remove the repository from all Draft releases before it can be unassigned from the project.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The release creation wizard MUST allow the admin to select which of the project's assigned repositories are included in the release.
- **FR-002**: The system MUST pre-select repositories that have commits since their last tag, and leave repositories with no new commits unchecked by default.
- **FR-003**: For each selected repository, the system MUST suggest a next version and bump type derived from the dominant change type (`major` / `minor` / `patch`) in the commit range since the last tag.
- **FR-004**: The suggested next version and bump type MUST be editable before submission; changing the bump type radio MUST recompute the version field; manually editing the version field MUST switch the radio to `custom`.
- **FR-005**: The system MUST validate that each entered next version is a valid semver string strictly greater than the repo's previous version, and MUST block submission if this constraint is violated.
- **FR-006**: At least one repository MUST be selected; submission with zero repos MUST be blocked.
- **FR-007**: The release-level version label MUST be derived automatically from the primary repo's next version when the primary repo is included; if not included, it falls back to the alphabetically-first included repo's next version.
- **FR-008**: The version label MUST NOT be directly editable by the user; it MUST update live as the primary repo's next version changes.
- **FR-009**: At the moment of release creation, the system MUST capture and store immutably per-repo: previous version, next version, bump type, from-commit SHA, to-commit SHA, commit count, and ticket count.
- **FR-010**: These snapshot fields MUST NOT be re-derived on later views; they are frozen at creation time.
- **FR-011**: While a release is in `Draft` status, the admin MAY update the repo selection and per-repo versions; all snapshot fields MUST be re-captured server-side on each save.
- **FR-012**: Once a release is `Published`, the repo selection and per-repo versions MUST be frozen; only release notes and Confluence URL remain editable.
- **FR-013**: The Project page MUST display a Releases section listing all releases for the project with: name, version label, creation date, status, and repo count.
- **FR-014**: The Releases list MUST default to most-recently-created-first sort and MUST support filtering by status and searching by name.
- **FR-015**: The Release detail page MUST render a per-repo table from the stored snapshot, showing each repo's previous → next version, bump type, commit count, and ticket count.
- **FR-016**: The system MUST block removal of a repository from a project while that repository is included in any Draft release for that project; the admin MUST first remove the repository from all Draft releases before unassignment is permitted.
- **FR-017**: When an admin opens a Draft release for editing, the system MUST acquire a pessimistic edit lock on that release with a TTL of 10 minutes, refreshed on activity. A second admin attempting to open the same Draft for editing MUST be shown a "currently being edited by [name]" notice and blocked from editing until the lock expires or is explicitly released.

### Key Entities

- **Release**: The existing release entity, with `Version` now derived from the primary repo's next version at creation/save time. Gains a collection of `ReleaseRepository` join records.
- **ReleaseRepository**: Join entity linking a release to a repository, storing the versioning snapshot (previous version, next version, bump type, from/to commit SHAs, commit count, ticket count). Immutable once the release is Published.
- **VersionBumpSuggestion**: A transient value computed per-repo at wizard load time, representing the suggested next version, bump type, and commit range metadata. Never persisted directly — used only to pre-fill the wizard.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A tech lead can complete the multi-repo release creation flow — selecting repos, confirming per-repo versions, and submitting — in under 3 minutes for a project with up to 10 repositories.
- **SC-002**: Per-repo snapshot data is visible on the release detail page within 2 seconds of navigating to it, for releases with up to 20 included repositories.
- **SC-003**: 100% of snapshot fields (previous version, next version, bump type, commit SHAs, counts) are preserved unchanged when a published release is viewed after its source repo tags have been deleted or the repo has been removed from the project.
- **SC-004**: The version suggestion logic correctly identifies the dominant change type (`major` / `minor` / `patch`) for at least 95% of repositories evaluated against a standard set of conventional-commit scenarios.
- **SC-005**: The Releases list on the project page loads all releases for a project with up to 100 releases within 2 seconds.
- **SC-006**: Admins report in usability testing that the per-repo version selection step is understandable without documentation (target: 80% task-completion rate on first attempt).

## Assumptions

- The feature builds on the existing `ProjectRepository` join table; no new repo-assignment surface is introduced.
- A repository that was assigned to the project at release creation time but is later removed from the project still has its snapshot data visible on the release detail page (snapshot is immutable).
- The existing conventional-commit parsing logic is already capable of detecting breaking changes, `feat`, `fix`, and other types; this feature reuses it per-repo without modifying the parser.
- Non-semver repositories (CalVer, date-based) are out of scope for v1; the existing semver-only constraint applies.
- Cross-project releases (a release spanning multiple logical projects) are out of scope.
- Automatic Git tag creation on publish is out of scope; the existing publish flow is unchanged.
- Bulk operations on the Releases list (multi-delete, export) are out of scope for v1.
- Existing edit-permission rules (Admin writes, Viewer reads) apply unchanged to the new surfaces.
- Legacy releases (created before this feature) will be backfilled with a single `ReleaseRepository` row pointing to the primary repo, with empty snapshot fields; the UI must handle and label this gracefully.

## Clarifications

### Session 2026-05-23

- Q: When an admin removes a repo from a project while it is included in a Draft release, what should happen? → A: Block unassignment; the admin must remove the repo from all Draft releases first.
- Q: When two admins try to edit the same Draft release simultaneously, what should happen? → A: Pessimistic lock with 10-minute TTL; second admin is blocked with a "currently being edited by [name]" message.
