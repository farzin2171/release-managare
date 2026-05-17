# Feature Specification: Project Screen — Repository Sync & Changes Persistence

**Feature Branch**: `003-project-repo-sync`  
**Created**: 2026-05-16  
**Status**: Draft  
**Input**: User description: "Project Screen — Repository Sync & Changes Persistence"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single Repository Sync (Priority: P1)

As a Tech lead (Viewer or Admin), I can trigger a sync for an individual repository from its card on the project screen. The sync fetches all commits between the repository's pinned latest tag and the current HEAD on the default branch, extracts Jira tickets, breaking changes, and contributor data, and persists the results so the card immediately shows updated metrics.

**Why this priority**: Delivers the core "sync and see" value in isolation; every other story depends on or builds on this one.

**Independent Test**: Can be tested by opening a project screen with at least one repository that has a pinned tag, clicking "Sync" on that card, and verifying the four metrics update and persist correctly — without any other story being implemented.

**Acceptance Scenarios**:

1. **Given** a repository card with a pinned latest tag and the "Sync" button enabled, **When** the user clicks "Sync", **Then** the card enters the "in progress" state with a live status message and a running timer, and the Sync button is disabled.
2. **Given** a sync completes successfully, **When** the card returns to idle, **Then** the four metrics (commits, tickets, breaking changes, contributors) display actual values derived from the commit range, and the footer shows a "last synced" relative timestamp.
3. **Given** a sync fails (e.g., the access token expired), **When** the card returns to idle, **Then** the card background turns red, a brief error reason is shown, and the footer button reads "Retry".
4. **Given** a repository card with no pinned latest tag, **When** the user views the card, **Then** the Sync button is disabled and the card body shows "Pin a latest tag to enable sync".
5. **Given** a sync has been run before on the same repository and tag, **When** the user triggers sync again, **Then** no duplicate commit or ticket records are created; existing records are updated in place.

---

### User Story 2 - Project-Wide Sync (Priority: P2)

As a Tech lead, I can start a single "Sync project" action from the project screen that automatically syncs every assigned repository one by one. I can watch progress live on each card and cancel the run if needed.

**Why this priority**: Saves the user from clicking "Sync" on each card individually; delivers significant time savings for projects with many repositories. Depends on Story 1 being complete.

**Independent Test**: Can be tested by opening a project screen with multiple repositories (some with pinned tags, some without), clicking "Sync project", and verifying each card progresses through states in sequence, repositories without tags are skipped, a failed repo does not halt the run, and the strip at the top reflects the running count.

**Acceptance Scenarios**:

1. **Given** a project with multiple repositories, **When** the user clicks "Sync project", **Then** repositories are synced sequentially in their listed order; only the currently-syncing card is in the "in progress" state; all others remain idle.
2. **Given** a project sync is running and a repository has no pinned tag, **When** that repository is reached in the queue, **Then** it is automatically skipped and the run continues to the next repository; the skipped card remains in the "No pinned tag" state.
3. **Given** a project sync is running and one repository sync fails, **When** the failure occurs, **Then** the failed card enters the "Failed" state and the run continues with the next repository.
4. **Given** a project sync is in progress, **When** the user clicks "Cancel", **Then** the currently-in-flight repository completes its sync, and no further repositories are started; the strip shows the final partial results.
5. **Given** a project sync is already running, **When** another user attempts to start a second project sync, **Then** the action is rejected and the user is informed that a run is already in progress.
6. **Given** a project sync completes, **When** all repositories have been processed, **Then** the strip shows the final summary (e.g., "16 of 18 synced · 2 skipped") and the four top stat cards animate to the updated totals.

---

### User Story 3 - Persisted State on Return Visit (Priority: P3)

As a Tech lead, when I return to the project screen after a previous sync, I immediately see the last-synced metrics for every repository without triggering a new sync or waiting for any external data fetch.

**Why this priority**: Significantly improves daily usability; removes the need to re-sync just to read data that was already captured. Depends on Story 1 persisting data correctly.

**Independent Test**: Can be tested by performing a sync, navigating away from the project screen, and returning — verifying all card metrics and the "Project last synced" strip reflect the previously persisted data and no external call is triggered.

**Acceptance Scenarios**:

1. **Given** one or more repositories have been successfully synced, **When** the user opens the project screen, **Then** each synced card displays its persisted metric values (commits, tickets, breaking changes, contributors) and a relative "last synced" timestamp.
2. **Given** a repository has never been synced, **When** the user views its card, **Then** all four metrics read "0" and the footer shows "Not synced yet".
3. **Given** at least one project-level sync has completed, **When** the user opens the project screen, **Then** the "Project last synced" strip shows the relative timestamp of the most recent successful project run.
4. **Given** no project-level sync has ever been run, **When** the user opens the project screen, **Then** the strip shows "Never synced" with the "Sync project" button as the primary call to action.
5. **Given** all data is persisted, **When** the screen loads, **Then** none of the displayed metrics originate from a live call to the Git provider.

---

### User Story 4 - Contributor Visibility (Priority: P4)

As a Tech lead, I can click the "contributors" metric on a repository card to see a list of individual contributors and their commit counts for the current release range.

**Why this priority**: Adds insight value for release planning and attribution; does not block other stories and is a self-contained enhancement.

**Independent Test**: Can be tested by syncing a repository and clicking the "contributors" metric number on the card to verify a popover appears with names and per-contributor commit counts.

**Acceptance Scenarios**:

1. **Given** a repository has been synced and has contributors, **When** the user clicks the "contributors" number on the card, **Then** a popover opens anchored to the card showing each contributor's display name and commit count.
2. **Given** two repositories share contributors (same email), **When** the top "CONTRIBUTORS" stat card is viewed, **Then** the count reflects the union of unique contributors across all synced repositories, with duplicates removed.
3. **Given** a contributor has no email on record, **When** their data is displayed in the popover, **Then** their display name is used as the unique identifier instead.

---

### Edge Cases

- What happens when a repository's pinned tag has been deleted from the Git provider between syncs? The sync fails gracefully and shows a descriptive error on the card (e.g., "Tag not found"). This is a permanent error — no automatic retry.
- What happens when the Git provider returns a transient error (rate-limit or brief outage) mid-sync? The system retries up to 3 times with a short pause. If all 3 attempts fail, the sync is marked failed and the card enters the Failed state.
- What happens when a repository has zero commits between the pinned tag and HEAD? The sync succeeds, storing zero counts; the card shows "0" metrics and a valid "last synced" timestamp.
- What happens when a project-level sync is interrupted by a server restart mid-run? On restart, any sync record left in "in progress" state for more than 30 minutes is automatically marked as failed.
- What happens when a contributor has a "noreply" or empty email? The system uses the contributor's display name as a fallback deduplication key.
- What happens when "Cancel" is clicked after the last repository in the run has already started syncing? The in-flight repository completes and the run finishes normally; "Cancel" is effectively a no-op.
- What happens when the top stat cards are viewed after only some repositories have been synced? The totals aggregate only the synced repositories; unsynced repositories contribute zero to all counts.
- What happens when the pinned tag on a repository is changed after a sync has already been run? The card immediately reverts to the "Not synced yet" state (all-zero metrics, "Not synced yet" footer) because the previous sync record belongs to the old tag. No data is deleted; the old records are simply no longer the active snapshot.
- What happens when two users simultaneously click "Sync" on the same repository card? The second request is rejected by the system with a clear message ("Sync already in progress for this repository"); only one active sync runs at a time per repository (FR-024).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each repository card on the project screen MUST display a "Sync" button in its footer accessible to users with Viewer or Admin roles.
- **FR-002**: The "Sync" button MUST be disabled (with an explanatory tooltip) when the repository has no pinned latest tag set.
- **FR-003**: When a user triggers a single-repository sync, the system MUST fetch all commits between the repository's pinned tag and its current HEAD on the default branch, then extract and persist the commit count, unique Jira ticket count, breaking change count, contributor count, and contributor details.
- **FR-004**: Sync operations MUST be idempotent — re-syncing the same repository and tag combination MUST update existing records rather than creating duplicate entries.
- **FR-005**: After a successful single-repository sync, the card MUST display updated metrics and a "last synced" relative timestamp without requiring a page reload.
- **FR-006**: After a failed single-repository sync, the card MUST display a brief error reason and change the footer button to "Retry".
- **FR-007**: The project screen MUST include a "Project last synced" strip between the project header and the stat cards, showing the timestamp of the most recent project-level sync run.
- **FR-008**: Before any project-level sync has been run, the strip MUST show "Never synced" with "Sync project" as the primary call to action.
- **FR-009**: The "Sync project" button MUST trigger a sequential sync of all assigned repositories in their listed order.
- **FR-010**: During a project-level sync run, the strip MUST display a live progress counter (e.g., "Syncing… 3 of 18 complete") and the button MUST change to "Cancel".
- **FR-011**: Repositories without a pinned latest tag MUST be automatically skipped during a project-level sync run; the run MUST continue to the next repository.
- **FR-012**: A single repository failure during a project-level sync run MUST NOT stop the run; the run MUST continue to the next repository.
- **FR-013**: When "Cancel" is clicked during a project-level sync, the currently-syncing repository MUST be allowed to complete before the run stops.
- **FR-014**: The system MUST prevent concurrent project-level sync runs for the same project; a second attempt MUST be rejected with a clear message to the user.
- **FR-015**: On completion of a project-level sync, the strip MUST show the final summary (counts of synced, skipped, and failed repositories) and the four top stat cards MUST animate to updated totals.
- **FR-016**: On project screen load, all repository card metrics MUST be sourced from the latest successful sync record for the repository's **current pinned tag** — no live call to the Git provider is required to display metrics. If the pinned tag has changed since the last sync, the card MUST revert to the "Not synced yet" state until a new sync is run against the new tag.
- **FR-017**: The "contributors" metric on each repository card MUST be clickable and MUST open a popover listing each contributor's display name and commit count.
- **FR-018**: The total "CONTRIBUTORS" count in the top stat cards MUST represent the unique union of all contributors across synced repositories, deduplicated by lowercased email (with display name as fallback when email is absent).
- **FR-019**: Each repository card MUST visually indicate its current sync state through distinct styling — neutral (idle), highlighted (in progress), and error-highlighted (failed).
- **FR-020**: During an in-progress sync on a repository card, the metric area MUST display a live status message that progresses through the sync phases (e.g., "Fetching commits…" → "Parsing commits…" → "Persisting tickets…" → "Finalising…").
- **FR-021**: The "Project last synced" strip MUST show a live progress indicator and running elapsed time while a project sync is in progress, and MUST revert to its normal state automatically after the run completes.
- **FR-022**: After a single-repository sync completes successfully (whether triggered standalone or not as part of a project run), the four top stat cards (Total Commits, Unique Tickets, Breaking Changes, Contributors) MUST immediately refresh to reflect the updated project-level totals.
- **FR-023**: When the Git provider returns a transient error (e.g., rate-limit or temporary service unavailability) during a sync, the system MUST automatically retry up to 3 times with a brief pause between attempts before declaring the sync failed; permanent errors (e.g., invalid credentials, resource not found) MUST NOT be retried.
- **FR-024**: The system MUST prevent concurrent sync runs for the same repository; if a sync is already in progress for a given repository, any subsequent sync request for that same repository MUST be rejected with a clear message (e.g., "Sync already in progress for this repository").

### Key Entities *(include if feature involves data)*

- **Repository Sync Record**: Represents a single sync operation for a (repository, pinned tag) pair. Key attributes: status (pending / in-progress / succeeded / failed / skipped), metric counts (commits, tickets, breaking changes, contributors), contributor details snapshot, current processing phase, timestamps (started, completed), optional error message, and optional link to a parent project sync run. The **current snapshot** for a card is the latest successful record where the pinned tag matches the repository's currently configured pinned tag; records for previous tags are retained but not displayed.
- **Project Sync Record**: Represents a single project-level sync run. Key attributes: status (pending / in-progress / succeeded / partially failed / failed / cancelled), counts of total / succeeded / failed / skipped repositories, and timestamps (started, completed).
- **Contributor Snapshot**: A denormalised list of contributors captured at sync time, stored within the Repository Sync Record. Each entry includes display name, email (or name as fallback), and commit count for the sync range.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A user can trigger a single-repository sync and see updated metrics on the card within 30 seconds for repositories with up to 500 commits.
- **SC-002**: After a project-level sync completes, the project screen loads and displays all persisted metrics within 3 seconds without any external Git provider calls.
- **SC-003**: Syncing the same repository and tag twice in succession produces identical metric counts — zero duplicate records created in any scenario.
- **SC-004**: A project-level sync of 20 repositories (all with pinned tags and up to 200 commits each) completes end-to-end in under 10 minutes.
- **SC-005**: 100% of repository cards on the project screen load their persisted metrics from the database on first render — no card requires a live Git provider call to display data.
- **SC-006**: The "Cancel" action on a running project sync stops processing within one repository's sync duration — no additional repositories are started after the in-flight one completes.
- **SC-007**: The contributor popover correctly de-duplicates contributors across repositories — no contributor appears twice in the project-level total when they committed to more than one repository.

## Assumptions

- The "pinned latest tag" feature (which sets the starting point for each sync) is already implemented and functional in the application.
- Both Viewer and Admin roles can trigger sync operations; sync is treated as a read-replication of upstream data, not a privileged write operation.
- The conventional commit parsing logic that extracts ticket IDs, breaking-change flags, and author information is already implemented and is reused without modification.
- Each repository has a single default branch; multi-branch sync is out of scope for this version.
- Sync is manual-only in this version; there is no automatic or webhook-driven sync.
- Only commit metadata (SHA, author, date, message) is fetched — no diff content or file trees.
- The project screen's existing layout (header, stat cards, repository grid, card metric grid) is unchanged; all additions are purely additive.
- The "View run" drawer for a project sync shows a detail view of the last project sync run; no new workflow is initiated from it.
- Polling at approximately two-second intervals is sufficient for single-repository sync progress updates; real-time streaming is reserved for project-level syncs.
- Stale sync records (left in "in progress" state for more than 30 minutes after a server restart) are automatically marked as failed on worker restart, with an appropriate error message.
- Cancellation completes the current in-flight repository before stopping; there is no hard-abort of a mid-flight sync.

## Clarifications

### Session 2026-05-16

- Q: After a single-repository sync succeeds, should the four top stat cards immediately refresh to reflect the new totals? → A: Yes — top stat cards refresh immediately after every successful single-repo sync (added FR-022).
- Q: When the Git provider returns a transient error during a sync, should the system retry automatically before declaring failure? → A: Auto-retry up to 3 times with brief backoff; fail permanently only after all retries exhausted (added FR-023; updated edge cases).
- Q: Should the system enforce that only one active sync can run at a time per repository, and how should a second concurrent request be handled? → A: System rejects second concurrent request with a clear message ("Sync already in progress for this repository") (added FR-024; added edge case).
- Q: Which sync record determines the metrics shown on a repository card when multiple runs exist? → A: Latest successful sync for the current (repository, pinned tag) pair; changing the pinned tag resets the card to "Not synced yet" (updated FR-016, Key Entities, and edge cases).
