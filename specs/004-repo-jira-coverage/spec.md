# Feature Specification: Per-Repo Jira Ticket Visibility

**Feature Branch**: `004-repo-jira-coverage`
**Created**: 2026-05-17
**Status**: Clarified
**Input**: User description: "Per-Repo Jira Ticket Visibility on Project & Service Pages — always-on Jira comparison view surfacing coverage drift between commits since last tag and Jira fix version tickets"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Project Page Coverage Overview (Priority: P1)

An Admin navigates to a project's detail page and immediately sees Jira coverage health for every assigned repository — no release in flight required. Cards are sorted worst-first so attention goes to the most problematic repos. A project-wide aggregate header gives an at-a-glance health summary.

**Why this priority**: The primary value of this feature is continuous drift visibility. The project page is the natural entry point for admins doing daily health checks. Without this story, the feature delivers no user value.

**Independent Test**: Navigate to a project detail page with at least two assigned repositories that have different match rates. Verify cards appear sorted by match rate ascending, counters are correct, and the aggregate header reflects the project totals.

**Acceptance Scenarios**:

1. **Given** a project with three assigned repositories having match rates of 40%, 75%, and 95%, **When** an Admin navigates to the project detail page, **Then** cards appear in order 40% → 75% → 95%, each showing the repo name, current tag, computed fix version string, three counters, match rate percentage, and coloured health pill (red / amber / green respectively).
2. **Given** the same three repos, **When** the Admin clicks the sort toggle, **Then** cards reorder alphabetically by repository name.
3. **Given** a project page is loaded, **When** the page renders, **Then** an aggregate header appears above the cards showing total repo count, count with green health, count with amber+red health, and project-wide weighted match rate.
4. **Given** a repo card is displayed, **When** the Admin clicks "View details", **Then** the browser navigates to the service page with the "Jira coverage" tab active.
5. **Given** a repo card is displayed, **When** the Admin hovers the re-sync icon, **Then** a tooltip shows the last-synced timestamp for that repo.

---

### User Story 2 — Repository Page Jira Coverage Tab (Priority: P2)

An Admin or Viewer navigates to a repository's detail page and opens the "Jira coverage" tab to review the full three-bucket breakdown — which tickets are in both Git and Jira, which are Jira-only, and which are Git-only — along with unmatched commits that reference no Jira ticket.

**Why this priority**: Detailed per-repo investigation is the action triggered from the project page. This story delivers the depth that Story 1's cards summarise.

**Independent Test**: Navigate to a repository detail page, open the "Jira coverage" tab, and verify all four sections (header strip, summary cards, three-bucket breakdown, unmatched commits panel) are present with correct data.

**Acceptance Scenarios**:

1. **Given** a repository with five commits since last tag referencing three Jira tickets, and two additional tickets in the Jira fix version, **When** the "Jira coverage" tab loads, **Then** summary cards show: commits=5, git-tickets=3, Jira-tickets=5 (3 in both + 2 Jira-only), match rate=60%.
2. **Given** the tab is open, **When** the "In both" bucket has five or more items, **Then** the bucket is collapsed by default; when fewer than five, it is expanded.
3. **Given** the tab is open, **When** the "Jira only" or "Git only" buckets have items, **Then** they are expanded by default.
4. **Given** a Viewer is logged in, **When** they navigate to the "Jira coverage" tab, **Then** the full breakdown is visible but the "Re-sync" button and "Add to fix version" actions are absent.
5. **Given** there are commits with no parseable Jira ticket ID, **When** the "Unmatched commits" panel is expanded, **Then** each such commit's SHA, author, and message are listed.

---

### User Story 3 — Add Git-Only Ticket to Jira Fix Version (Priority: P3)

An Admin sees a ticket in the "Git only" bucket and wants to add it to the Jira fix version so it appears in both buckets and improves the match rate. They click "Add to fix version" inline, confirm, and the bucket updates.

**Why this priority**: This is the primary remediation action the feature enables. Without it, the feature is read-only; with it, admins can close coverage gaps without leaving the platform.

**Independent Test**: With a ticket in the "Git only" bucket, click "Add to fix version", confirm the action in the dialog, and verify the ticket moves to the "In both" bucket and match rate increases.

**Acceptance Scenarios**:

1. **Given** an Admin is viewing the "Git only" bucket with ticket PROJ-123, **When** they click "Add to fix version" and confirm the dialog, **Then** the Jira fix version `<RepoName>_<NextVersion>` is updated (created if it didn't exist), the ticket moves to the "In both" bucket, and the match rate recalculates.
2. **Given** the Jira fix version does not yet exist, **When** the Admin adds a ticket to it, **Then** the fix version is created automatically in Jira and the action succeeds.
3. **Given** the "Add to fix version" action is in flight, **When** the confirmation dialog is showing, **Then** a confirmation message explicitly states the Jira fix version name the ticket will be added to.

---

### User Story 4 — Force Re-sync (Priority: P4)

An Admin has just pushed commits or updated Jira tickets and wants the coverage data to reflect the change without waiting for the background refresh cycle. They click the "Re-sync" button and within seconds see updated data.

**Why this priority**: Cache freshness is user-controlled in the Admin path. Without re-sync, admins cannot verify that their remediation actions (adding tickets, pushing commits) have registered correctly.

**Independent Test**: With stale cached data, click "Re-sync" and verify the displayed data updates to reflect current Git commits and Jira fix version tickets.

**Acceptance Scenarios**:

1. **Given** cached coverage data is up to 4 minutes old, **When** an Admin clicks "Re-sync", **Then** fresh data is fetched from both Git and Jira, the display updates, and `LastSyncedAt` changes to the current time.
2. **Given** a re-sync is in flight, **When** the button is clicked, **Then** the button shows a loading state and cannot be clicked again until the request completes.
3. **Given** a Viewer is on the repository page, **When** they view the "Jira coverage" tab, **Then** the "Re-sync" button is not present but the last-synced timestamp is visible.

---

### Edge Cases

- What happens when the project page loads and no repo snapshots are warm (first-ever load)? → The project endpoint responds immediately; repos with no cached snapshot are returned with `health: Unknown` and `supported: false` as a sentinel. The client renders a `Skeleton` card for each cold repo and fires individual per-repo `GET /repositories/{id}/jira-coverage` requests in parallel to hydrate them progressively. SC-001 (< 2 s) applies only when the cache is warm.
- What happens when a repository has no semver tag? → System displays `0.1.0` as the next version with an "Untagged" indicator next to the fix version string; comparison proceeds normally.
- What happens when the latest tag is not semver (e.g., `release-2026-05-01`)? → System displays a warning pill on the card and disables Jira comparison for that repo; the tab shows an explanatory message rather than the breakdown.
- What happens when the computed Jira fix version does not exist in Jira? → Comparison shows zero Jira tickets with an informational note stating the fix version name and that it will be created on first publish or manual creation.
- What happens when the Jira connection fails during a re-sync? → An error state is shown on the card/tab; `LastSyncedAt` is not updated; the previous cached data remains visible with a staleness warning.
- What happens when the same ticket appears in multiple repos' fix versions? → Each repo evaluates independently; no de-duplication across repos.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST compute the next-release fix version for each tracked repository as `<RepositoryName>_<NextMinorVersion>` where `NextMinorVersion` increments the minor segment and resets patch to zero.
- **FR-002**: System MUST surface Jira ticket coverage for each tracked repository at all times, not only during release creation.
- **FR-003**: Project detail page MUST display a per-repo coverage card for each assigned repository showing: name, current tag, computed fix version (clickable to Jira), commits since last tag, unique Jira tickets referenced by commits, tickets in the Jira fix version, match rate percentage, health pill (green/amber/red), three-bucket strip (counts only), "View details" link, and re-sync icon with last-synced timestamp on hover.
- **FR-004**: Cards on the project detail page MUST be sorted by match rate ascending (worst first) by default, with a toggle to switch to alphabetical order.
- **FR-005**: Project detail page MUST display an aggregate header above the cards showing: total repository count, repositories with green health, repositories with amber+red health, and project-wide weighted match rate.
- **FR-006**: Repository detail page MUST include a "Jira coverage" tab visible to both Admin and Viewer roles.
- **FR-007**: "Jira coverage" tab MUST contain: header strip (repo name, current tag, fix version, health pill, re-sync button for Admin, last-synced timestamp), four summary cards (commits, git tickets, Jira tickets, match rate), three-bucket breakdown, and a collapsed unmatched commits panel.
- **FR-008**: Three-bucket breakdown MUST show "In both" (collapsed by default when ≥ 5 items), "Jira only" (expanded by default), and "Git only" (expanded by default).
- **FR-009**: "Git only" bucket MUST include an "Add to fix version" action for Admin users that adds the ticket to the computed Jira fix version. If the fix version does not exist, it MUST be created in the Jira project that owns the ticket (derived from the ticket key prefix, e.g., `PROJ-111` → project `PROJ`).
- **FR-010**: Match rate health MUST be calculated as: tickets in both ÷ union of both buckets; green ≥ 90%, amber 60–89%, red < 60%; `Unknown` when comparison is unsupported.
- **FR-011**: Comparison results MUST be cached per-repo (keyed by `repositoryId` + computed next version) with a 5-minute TTL.
- **FR-012**: A background job MUST refresh stale caches every 10 minutes for repositories viewed in the last 24 hours.
- **FR-013**: Admins MUST be able to force a refresh via a "Re-sync" button; last-synced timestamp MUST be visible to all roles.
- **FR-014**: Cache MUST be invalidated when: commits are re-synced, the repository's latest tag changes, or Jira connection settings change.
- **FR-015**: System MUST handle the "no tag" edge case: use `0.1.0` as next version and show an "Untagged" indicator.
- **FR-016**: System MUST handle the "non-semver tag" edge case: display a warning and disable Jira comparison until the convention is fixed.
- **FR-017**: System MUST handle a missing Jira fix version gracefully: show zero Jira tickets with an informational note.

### Key Entities *(include if feature involves data)*

- **RepoJiraComparisonSnapshot**: Cached per-repo comparison result; one row per `(RepositoryId, NextVersion)` pair; stores counts, match rate, and full bucket detail as JSON; keyed to avoid re-computing within the 5-minute TTL.
- **SemVer**: Domain value object for parsing a tag string into `(Major, Minor, Patch)`, computing `NextMinor()`, and formatting back to string; pure function, never persisted.
- **HealthBand**: Enumeration — `Green` (≥ 90%), `Amber` (60–89%), `Red` (< 60%), `Unknown` (non-semver or unsupported).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Project page with ten or more assigned repositories loads with Jira coverage data for all cards within 2 seconds when data is already cached. On first load (cold cache), each card appears immediately as a skeleton and hydrates progressively via individual per-repo requests; no single HTTP response blocks on multiple Jira API calls.
- **SC-002**: A forced re-sync completes and the UI reflects updated coverage within 30 seconds for any single repository.
- **SC-003**: Health pill colour accurately reflects the 90%/60% thresholds for all tested match rates including boundary values (exactly 60%, exactly 90%).
- **SC-004**: "Add to fix version" action succeeds and the affected ticket moves from the "Git only" bucket to the "In both" bucket within the same session without a full page reload.
- **SC-005**: All repositories with non-semver latest tags display a warning state rather than a broken or empty card.
- **SC-006**: Viewer-role users can view all coverage data on both pages without errors; Viewer-specific actions (re-sync, add-to-fix-version) are absent from the UI.

## Assumptions

- The Jira project key(s) associated with each repository are already stored in the platform's project configuration (from the existing Jira integration milestone).
- The `IJiraService` already has the capability to fetch tickets by fix version; if a `GetFixVersionByNameAsync` helper is missing, it will be added to the existing interface rather than creating a new one.
- `LastViewedAt` tracking on the `Repository` entity is introduced by this feature to support the background refresh scoping (repos viewed in last 24 hours).
- Concurrent re-sync calls for the same repository use last-write-wins semantics on the snapshot row; no optimistic locking required.
- The "Add to fix version" action targets only the computed next-release fix version (`<RepoName>_<NextVersion>`); it does not allow targeting arbitrary fix versions.
- When creating a Jira fix version (because it does not yet exist), the version is created in the Jira project that owns the ticket being added — derived from the ticket key prefix (e.g., `PROJ-111` → create in the `PROJ` project). The fix version is not created in other Jira project keys associated with the repository.
- No email or Slack alerts are in scope for v1; all feedback is on-screen.
- Non-semver versioning schemes (CalVer, date-based) are flagged as unsupported; the feature does not attempt to parse them.

## Clarifications

### Session 2026-05-17

- Q: How should the project page behave when all repo snapshots are cold (no cached data yet)? → A: Respond immediately from the project endpoint; cold repos return a sentinel (`health: Unknown`, no bucket data). The client renders a `Skeleton` card per cold repo and fires individual per-repo GET requests in parallel to hydrate progressively. SC-001's 2 s target applies to warm-cache loads only.
- Q: When creating a new Jira fix version via "Add to fix version", which Jira project does it belong to? → A: The Jira project that owns the ticket being added, derived from the ticket key prefix (e.g., `PROJ-111` → create in `PROJ`). Not broadcast to other repository-associated project keys.
