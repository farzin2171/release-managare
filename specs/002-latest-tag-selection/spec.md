# Feature Specification: Latest Tag Selection for Repositories

**Feature Branch**: `002-latest-tag-selection`
**Created**: 2026-05-15
**Status**: Draft
**Input**: User description: "Add the ability for an Admin to view all available Git tags for a tracked repository and explicitly pin one of them as the repository's latest tag."

## Clarifications

### Session 2026-05-15

- Q: When a repository is set to `IsTracked = false`, what should happen to its pinned latest tag? → A: Preserve the pinned tag; untracking has no effect on `LatestTag`. The data is retained so that if the repo is re-tracked later, the pin remains valid without requiring the Admin to re-pin.
- Q: What should the project screen tooltip show in the "pinned by" field when the original Admin's account has been deleted or deactivated? → A: Show "Unknown user" as the pinned-by value.
- Q: Should `LatestTagCommitSha` be stored in v1 even though force-move detection is out of scope? → A: Yes — store it now (one nullable column) so the v2 force-move detection feature requires zero additional migration. No UI surface or detection logic in v1; the field is returned in the API response DTO only.
- Q: Should the stale-tag warning (FR-011) trigger automatically when the panel opens, or only when "Fetch tags" is clicked? → A: Only when "Fetch tags" is clicked. Opening the panel must not trigger a live provider call; the warning appears alongside the freshly fetched tag list.
- Q: Should the amber dot (FR-014) appear for all repositories with no pinned tag, or only after some minimum tracked period? → A: All unset repositories get the amber dot unconditionally — a single null check on `LatestTag`. No time-based or sync-count conditions.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Admin Pins a Latest Tag for a Repository (Priority: P1)

An Admin opens a tracked repository's settings, fetches the live list of tags from the remote Git provider, selects one tag as the "latest tag", and saves it. The pinned tag is stored with the commit SHA, timestamp, and the identity of the Admin who set it.

**Why this priority**: This is the foundational action that all downstream features depend on. Until at least one repository has a pinned latest tag, the "changes since last tag" view cannot produce a deterministic baseline.

**Independent Test**: Can be fully tested by an Admin opening the repository detail panel, fetching tags, selecting one, confirming the save, and verifying the pinned tag appears with its metadata (commit SHA, date pinned, who pinned it).

**Acceptance Scenarios**:

1. **Given** an Admin is on the Settings → Repositories page, **When** they click a tracked repository row, **Then** a detail panel opens showing either the currently pinned latest tag with a "Last set: X ago by Y" line, or "Not set" if none has been pinned.
2. **Given** the repository detail panel is open, **When** the Admin clicks "Fetch tags", **Then** the system retrieves the live tag list from the remote provider and displays each tag with its name, short commit SHA, commit date, and author name.
3. **Given** the tag list is displayed, **When** the Admin selects a tag and clicks "Confirm", **Then** the tag is saved as the pinned latest tag for the repository and the panel updates to show the new pinned tag.
4. **Given** a pinned latest tag exists, **When** the Admin clicks "Clear", **Then** a confirmation dialog appears; on confirmation, the pinned tag is removed and the panel shows "Not set".
5. **Given** a Viewer opens the repository detail panel, **Then** they see the current pinned tag (or "Not set") but no "Fetch tags", "Select", or "Clear" buttons are shown.

---

### User Story 2 - Viewer Sees Each Repository's Latest Tag on the Project Screen (Priority: P2)

Any authenticated user can see the pinned latest tag for each repository directly on the project detail screen, without opening repository settings.

**Why this priority**: The project screen is the primary daily-use surface. Showing the pinned tag there gives the whole team — not just Admins — instant visibility into the baseline each repository is anchored to.

**Independent Test**: Can be fully tested by an Admin pinning a tag for a repository, then a Viewer opening the associated project's detail screen and confirming the "Latest tag" column displays the correct tag with tooltip details.

**Acceptance Scenarios**:

1. **Given** a project with repositories, **When** any authenticated user opens the project detail screen, **Then** a "Latest tag" column appears in the repositories table showing the pinned tag name as a visually distinct badge, or "—" if not set.
2. **Given** a repository with a pinned latest tag, **When** the user hovers over the tag badge, **Then** a tooltip appears showing the commit SHA (first 7 characters), commit date, and the email of the Admin who pinned it.
3. **Given** a repository with no pinned latest tag, **When** the user views the project screen, **Then** the repository row shows an amber visual indicator signalling that the baseline is not yet configured.

---

### User Story 3 - Tag List Reflects the Current Remote State (Priority: P3)

When an Admin fetches tags for a repository, the system always retrieves the live list from the Git provider rather than a cached copy, so the Admin never pins a tag that has since been deleted or replaced on the remote.

**Why this priority**: Stale tag data would result in invalid baselines. Freshness is a correctness requirement for this feature, not a performance optimisation.

**Independent Test**: Can be fully tested by verifying that two successive "Fetch tags" actions for the same repository each trigger a live provider call, and that a provider error surfaces as a user-visible error with a Retry option.

**Acceptance Scenarios**:

1. **Given** an Admin clicks "Fetch tags", **When** the provider call is in flight, **Then** a loading indicator is shown and all interactive controls in the tag list are disabled.
2. **Given** an Admin fetches tags, **When** the provider call fails (auth error, network timeout, or repo not found), **Then** a clear error message is displayed with a Retry button; no stale list is shown.
3. **Given** a repository has a pinned tag, **When** the Admin clicks "Fetch tags" and the returned live tag list does not contain the pinned tag name, **Then** a warning banner is displayed above the tag list: "The pinned tag is no longer present in the remote repository. Please select a new one." The panel open itself does not trigger this check.

---

### Edge Cases

- What happens when a tracked repository has no tags at all? The tag list shows empty with a message indicating no tags are available in the remote.
- What happens when the repository is set to "untracked" while it has a pinned latest tag? The pinned tag and all associated metadata are preserved unchanged — `IsTracked = false` does not modify the latest-tag fields. Since untracked repositories cannot be assigned to projects, no amber-dot indicator will appear for them on the project detail screen. If the repo is later re-tracked, the previously pinned tag remains valid.
- What happens when two Admins attempt to pin a different tag for the same repository simultaneously? The last write wins; both saves succeed without an error, and the winning value is whichever write reached the server last.
- What happens if the tag list exceeds a large number of entries? The table virtualises rows to maintain smooth scrolling; the full list is available for search and sort.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an Admin to open a detail panel for any tracked repository from the Settings → Repositories page.
- **FR-002**: System MUST provide a "Fetch tags" action in the repository detail panel that retrieves the live tag list from the remote Git provider on demand, never from a cache.
- **FR-003**: System MUST display each fetched tag with: tag name, short commit SHA (7 characters), commit date, and author name.
- **FR-004**: System MUST allow sorting the tag list by date (descending by default) and filtering by tag name.
- **FR-005**: System MUST allow an Admin to select a tag and confirm it as the "latest tag" for the repository, persisting the tag name, full commit SHA (for future force-move detection in v2), the timestamp of the pinning action, and the identity of the Admin who performed it. The commit SHA is stored but not surfaced with any detection logic in v1.
- **FR-006**: System MUST prevent pinning a latest tag on a repository that is not currently tracked, returning an appropriate error to the user.
- **FR-007**: System MUST validate at write time that the selected tag still exists in the remote provider before persisting it.
- **FR-008**: System MUST allow an Admin to clear the pinned latest tag via a destructive-action confirmation dialog.
- **FR-009**: System MUST record an audit entry for every set and clear of the pinned latest tag, capturing the user identity, repository identity, old value, new value, and timestamp.
- **FR-010**: System MUST display a loading state while the provider tag fetch is in progress, and a human-readable error with a Retry button if the fetch fails.
- **FR-011**: System MUST display a warning when the Admin clicks "Fetch tags" and the currently pinned tag is not present in the returned live tag list. The panel opening alone MUST NOT trigger a provider call; no stale-tag check occurs until the Admin explicitly fetches.
- **FR-012**: System MUST add a "Latest tag" column to the project detail screen's repositories table, showing the pinned tag or a "—" placeholder if not set.
- **FR-013**: System MUST display a tooltip on the tag badge in the project screen showing the short commit SHA, commit date, and the email of the Admin who pinned it. If the pinning Admin's account has since been deleted, the tooltip MUST show "Unknown user" in place of the email.
- **FR-014**: System MUST apply an amber visual indicator to any repository row in the project detail screen that has no pinned latest tag, signalling incomplete configuration. The check is a single null test on the pinned tag field — no time-based or sync-count conditions apply.
- **FR-015**: System MUST restrict all write actions (pin, clear) to Admin-role users; Viewers may only read the pinned tag value.

### Key Entities

- **Repository** (extended): The existing repository record gains four new attributes: pinned tag name (nullable), pinned tag commit SHA (nullable), timestamp of when the tag was pinned (nullable), and the identity of the Admin who pinned it (nullable).
- **Repository Tag**: A transient value returned from the Git provider representing a single tag. Carries tag name, full commit SHA, commit date, and author name. Not persisted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An Admin can fetch the tag list, select a tag, and save it as the pinned latest tag in under 60 seconds from opening the repository detail panel.
- **SC-002**: The tag list loads and becomes interactive in under 5 seconds under normal network conditions.
- **SC-003**: 100% of write actions (pin and clear) that succeed are reflected on the project detail screen immediately on next page load, with no manual refresh required.
- **SC-004**: A Viewer can confirm the pinned latest tag and its commit SHA for any repository in any project without any Admin action beyond the initial pinning.
- **SC-005**: Every pin and clear action is recorded in the audit log with no exceptions; 0 write events are silently unlogged.

## Assumptions

- Only tracked repositories (those with `IsTracked = true`) are eligible for latest-tag pinning; this aligns with existing behaviour where untracked repositories are excluded from project workflows.
- The Git provider is Azure DevOps; the tag fetch maps to the existing refs API already used by the platform.
- Non-annotated (lightweight) tags are shown alongside annotated tags; the list is not filtered by tag type.
- The tag list is not paginated on the client side in v1, but the table virtualises rows when the list is large to maintain performance.
- Concurrent writes (two Admins pinning simultaneously) use last-write-wins semantics; optimistic locking is not required for this feature.
- The "changes since last tag" release baseline continues to use semver inference if no latest tag is pinned, preserving existing behaviour for repositories not yet configured.
- `LatestTagCommitSha` is stored at pin time to enable future (v2) force-move detection without a schema migration. No detection logic or SHA-mismatch UI exists in v1.
- Project screen export (if present) includes the latest tag column value in its output.
