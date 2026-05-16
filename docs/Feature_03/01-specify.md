# Feature: Project Screen — Repository Sync & Changes Persistence

> Paste this into Claude Code after `/specify` on a new feature branch.
> Spec Kit will produce `specs/NNN-project-sync/spec.md` from it.

## Feature Summary

Extend the existing project detail screen so each repository card shows its **pinned latest tag** (already implemented in the previous feature) and gains a new **"Sync"** action in the card footer. Syncing a repository fetches every commit between its pinned latest tag and `HEAD` on the default branch, parses each commit with the conventional-commits parser already in the codebase, extracts Jira tickets and contributors, and **persists the result** so that the project screen can render the rolled-up view from the database on subsequent loads — no Git provider call needed.

The screen also gains a **"Sync project"** action at the top that runs the per-repo sync sequentially for every assigned repository, with live progress reflected on each repo card.

This feature is purely additive. No existing tables, endpoints, layout, components, or styling change behaviour. The existing four-metric grid inside each repo card (commits / tickets / breaking / contributors) is reused — it simply renders non-zero values once a sync has run.

## Existing UI we are not changing

For clarity about what stays put:

- The page header with breadcrumb (`Projects / <Project>`), project dot, name, "— description", and the "New release" button on the right
- The "UNRELEASED CHANGES ACROSS ALL REPOSITORIES" section with its four stat cards (Total Commits, Unique Tickets, Breaking Changes, Contributors)
- The "REPOSITORIES (N)" section title with its count
- The two-column responsive grid of repository cards
- Each card's existing internal grid showing four metrics (commits / tickets / breaking / contributors)
- The blue navigation chrome at the top of the page

All of the above remain visually identical. The stat cards and per-card metrics already exist as zeros — they start showing real numbers once syncs populate the database.

## UI changes — three additive elements

### 1. New "Project last synced" strip
A thin strip sits **between the project title row and the "UNRELEASED CHANGES" stat cards**. It is a single rounded panel (matches existing card styling: white background, 0.5px border, `border-radius-md`, 8px vertical padding). It contains, left-to-right:

- `<i class="ti ti-refresh">` icon
- Label "Project last synced"
- Relative timestamp (e.g. "2h ago") in medium weight
- Bullet separator
- Roll-up summary: "16 of 18 synced · 2 skipped" (with a check icon)
- "View run" link (opens a small drawer with the last `ProjectSync` run detail)
- **"Sync project"** button on the far right with a refresh icon

The strip is always visible once the first project-level sync has run. Before that, it shows "Never synced" with the "Sync project" button as the primary call to action.

While a project sync is in progress, the strip transforms:
- The summary becomes "Syncing… 3 of 18 complete" with a spinner icon
- The "Sync project" button becomes "Cancel" with a stop icon
- Background tints to light blue (`var(--color-background-info)`)

When the project sync completes (success, partial, or failed), the strip stays in the completed state with a "Last run completed Xs ago" line that auto-dismisses after 30 seconds, reverting to the normal "Project last synced" view.

### 2. Repo card additions

Each repo card gets two small additions, no removals:

**Top-right tag chip** (replaces the existing `→ HEAD` text label):
- Format: `v2.4.1 → HEAD` rendered as a small pill (`font-mono`, 11px, `var(--color-background-secondary)` background, `border-radius-md`)
- When the repo has no pinned tag: chip reads `No tag → HEAD` in italic muted text, and a small **amber dot** sits next to the repo name as a subtle "needs attention" indicator
- Hovering the chip shows a tooltip with the tag's commit SHA (first 7 chars) and date

**Card footer strip** (new bottom section of every card, separated from the metric grid by a 0.5px border):
- Left: `<i class="ti ti-clock">` + relative last-synced timestamp (e.g. "2h ago"); "Not synced yet" when no successful sync has ever run for the pinned tag
- Right: a small **"Sync"** button with refresh icon

Adds ~30px to each card's height. The four-metric grid above it stays the same shape.

### 3. Repo card states during sync

The card itself is the progress indicator — no separate progress panel, no layout shift. Each card visually takes one of five states:

**Default (idle, never synced)** — neutral card, all four metrics read "0", footer says "Not synced yet" with an enabled "Sync" button.

**Default (idle, synced before)** — neutral card with real metric values, footer says "2h ago" with an enabled "Sync" button.

**In progress** — card background tints light blue (`#E6F1FB`), border becomes blue (`#85B7EB`). The four-metric grid is temporarily replaced by a centered live status line: spinner icon + "Parsing 34 commits…" (the message updates: "Fetching commits…" → "Parsing N commits…" → "Persisting tickets…" → "Finalising…"). A small "syncing" pill appears next to the repo name. Footer shows "running now · 12s" and the Sync button is disabled with a spinner.

**Failed** — card background tints light red (`#FCEBEB`), border becomes red (`#F09595`). The four-metric grid is replaced by a centered error line: alert icon + brief error reason (e.g. "PAT expired during sync"). Footer shows "failed 2h ago" with a "Retry" button in red.

**No pinned tag (sync disabled)** — neutral card, four-metric grid replaced by centered muted message "Pin a latest tag to enable sync". Footer left side becomes a link to "Open repository settings"; the Sync button is disabled.

After a failed or in-progress card returns to a terminal success state, the metric grid re-renders with the updated counts and the card reverts to neutral styling.

## User Stories

### Story 1 — Tech lead syncs a single repository
**As a** Tech lead (Viewer or Admin)
**I want to** click "Sync" on a repository card in the project screen
**So that** I can see up-to-date commits, tickets, contributors and breaking changes since the last pinned tag without leaving the page.

**Acceptance criteria**
- Each repository card has a "Sync" button in its footer
- The button is disabled with a tooltip ("Pin a latest tag first") when the repo has no `LatestTag` set
- Clicking "Sync" puts the card into the **In progress** state described above
- On success: the card reverts to **Default (idle, synced before)** with updated metric values and a fresh "Last synced just now" timestamp
- On failure: the card enters the **Failed** state; the button becomes "Retry"
- Sync is idempotent — re-running on the same `(repo, tag)` pair upserts; it does not duplicate commits or tickets
- Viewer and Admin can both trigger sync; the action is not Admin-gated

### Story 2 — Tech lead syncs the whole project
**As a** Tech lead
**I want to** click "Sync project" in the strip at the top of the project screen
**So that** every assigned repository is synced one by one and I can watch the progress directly on the cards.

**Acceptance criteria**
- The strip's "Sync project" button starts the run
- Repositories are synced **sequentially**, one at a time, in the order they appear in the project's repositories list
- The currently-syncing repo's card enters the **In progress** state; all other not-yet-started cards remain idle
- Repos without a `LatestTag` are auto-skipped — their card stays in the **No pinned tag** state and the project run continues to the next repo
- A repo failure does not stop the run; that repo's card enters the **Failed** state and the next repo begins
- During the run, the strip shows "Syncing… X of N complete" and the button reads "Cancel"
- Cancelling completes the in-flight repo then stops; remaining unsynced repos stay in their previous state
- When the run completes, the strip updates to show the final summary ("16 of 18 synced · 2 skipped", or with failure counts) and the project's "last synced" timestamp
- The project's stat cards at the top (Total Commits / Unique Tickets / Breaking Changes / Contributors) recompute and animate to the new totals on completion
- Concurrent project-level runs are prevented at the API level (409 returned on second attempt)

### Story 3 — Tech lead returns to the screen later
**As a** Tech lead
**I want to** open the project screen and immediately see the last-synced state for every repo
**So that** I don't have to wait for Git provider calls or trigger sync just to read data we already have.

**Acceptance criteria**
- On screen load, each card renders its four metrics from the persisted sync data (the `RepositorySyncs` snapshot + the existing `Commits` and `Tickets` rows)
- If a repo has never been synced, its card is in the **Default (idle, never synced)** state with all-zero metrics and "Not synced yet"
- The top stat cards aggregate across all repos that have a successful sync (repos never synced contribute zero)
- The "Project last synced" strip reads from the most recent successful `ProjectSync` run
- All three pieces of data — repo cards, top stat cards, strip — are sourced from the database, not from Azure DevOps

### Story 4 — Contributor visibility
**As a** Tech lead
**I want to** click the "contributors" number on a repo card and see who actually contributed
**So that** I know whose work is included in the upcoming release.

**Acceptance criteria**
- The "contributors" metric label on each card becomes a clickable target (cursor pointer, subtle hover state)
- Clicking opens a popover anchored to the card listing contributor display names and the count of commits each authored in this range
- The total contributor count in the top "CONTRIBUTORS" stat card is the union across all synced repos in the project, de-duplicated by lowercased email
- Contributor data is captured during sync and stored on `RepositorySync` as a denormalised JSON snapshot — no new entity required

## Data model changes

All additive. No existing tables are modified.

### New table: `RepositorySyncs`
One row per sync run for a `(Repository, FromTag)` pair. The latest successful row for a given `(RepositoryId, FromTag)` is the "current" snapshot rendered on the card.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `RepositoryId` | `Guid` | FK → `Repositories.Id` |
| `ProjectSyncId` | `Guid?` | FK → `ProjectSyncs.Id`, NULL when triggered standalone |
| `FromTag` | `string(100)` | snapshot of `Repository.LatestTag` at the time of sync |
| `ToCommitSha` | `string(64)` | HEAD SHA of the default branch at sync time |
| `Status` | `enum` | `Pending`, `InProgress`, `Succeeded`, `Failed`, `Skipped` |
| `SkipReason` | `string?` | populated when `Skipped` (e.g. "NoPinnedTag") |
| `StartedAt` | `DateTimeOffset` | UTC |
| `CompletedAt` | `DateTimeOffset?` | UTC, NULL while running |
| `CommitCount` | `int` | computed at completion |
| `TicketCount` | `int` | unique Jira tickets parsed from commits |
| `ContributorCount` | `int` | unique contributors by email |
| `BreakingChangeCount` | `int` | commits flagged breaking |
| `ContributorsJson` | `string` | denormalised snapshot: `[{ "name": "...", "email": "...", "commits": 3 }]` |
| `CurrentStep` | `string?` | populated while `InProgress` (e.g. "ParsingCommits") — feeds the live status line on the card |
| `ErrorMessage` | `string?` | populated on failure |
| `TriggeredByUserId` | `Guid` | FK → `Users.Id` |

Indexes:
- `(RepositoryId, FromTag, Status, StartedAt DESC)` — find latest snapshot per `(repo, tag)`
- `ProjectSyncId` — used to roll up project run

### New table: `ProjectSyncs`
One row per project-wide sync run.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `Guid` | PK |
| `ProjectId` | `Guid` | FK → `Projects.Id` |
| `Status` | `enum` | `Pending`, `InProgress`, `Succeeded`, `PartiallyFailed`, `Failed`, `Cancelled` |
| `StartedAt` | `DateTimeOffset` | UTC |
| `CompletedAt` | `DateTimeOffset?` | UTC |
| `TotalRepos` | `int` | number of assigned repos at start |
| `SucceededCount` | `int` | |
| `FailedCount` | `int` | |
| `SkippedCount` | `int` | |
| `TriggeredByUserId` | `Guid` | FK → `Users.Id` |

Indexes:
- `(ProjectId, Status, StartedAt DESC)` — find latest run per project
- Unique partial index where `Status IN ('Pending', 'InProgress')` on `ProjectId` — enforce single active run per project

### Reuse of existing tables
- `Commits` (already in spec) — sync writes parsed commits here, keyed by `(RepositoryId, Sha)`
- `Tickets` (already in spec) — sync writes/updates per-ticket aggregates here
- Existing `IConventionalCommitParser` is reused unchanged

## API endpoints (all new)

```
POST   /api/v1/repositories/{id}/sync                  → 202 Accepted, returns RepositorySync
GET    /api/v1/repositories/{id}/sync/latest           → 200, returns latest RepositorySync (or 404)
GET    /api/v1/repository-syncs/{syncId}               → 200, single sync detail

POST   /api/v1/projects/{id}/sync                      → 202 Accepted, returns ProjectSync (409 if running)
DELETE /api/v1/projects/{id}/sync/active               → 200, cancels the active project sync
GET    /api/v1/projects/{id}/sync/latest               → 200, latest ProjectSync with child RepositorySyncs
GET    /api/v1/projects/{id}/sync/active               → 200 with the in-progress run, or 204 No Content
GET    /api/v1/projects/{id}/sync/active/stream        → text/event-stream, SSE updates on the active run

GET    /api/v1/projects/{id}/repositories/sync-snapshot → 200, list of { repoId, latestSync, currentStep } — single call to render every card on screen load
```

The SSE stream emits one event per repository state transition (`pending → in_progress → succeeded/failed/skipped`) and per `CurrentStep` change for the in-flight repo. The final `complete` event carries the run summary. Reusing the .NET `Results.ServerSentEvents` API keeps this dependency-free.

Both Viewer and Admin can call all of these endpoints — sync is read-replication of upstream Git data, not a privileged operation.

## Background execution

- Sync runs on an in-process background worker using `IHostedService` + a `Channel<SyncJob>` queue (existing pattern, no new dependency)
- The HTTP `POST` endpoint enqueues the job and returns immediately with the created `RepositorySync` / `ProjectSync` row in `Pending` status
- The worker picks up the job, flips status to `InProgress`, runs it, persists the final state
- One sequential worker per project enforces "one project sync at a time"; per-repo standalone syncs can run in parallel across different repos
- `CurrentStep` is updated on the `RepositorySync` row at each phase boundary; the SSE channel publishes a corresponding event so the card's live status line updates without polling

## Conventional commit parsing reuse

The existing `IConventionalCommitParser` is the **only** source of truth for type, scope, ticket ID and breaking flag. The sync service:
1. Sets `CurrentStep = "FetchingCommits"`, fetches commits between `LatestTag` and HEAD via `IGitProviderService.ListCommitsAsync(from, to)` (already abstracted)
2. Sets `CurrentStep = "ParsingCommits"`, parses each commit
3. Sets `CurrentStep = "PersistingCommits"`, inserts/updates the `Commits` row keyed by `(RepositoryId, Sha)`
4. Sets `CurrentStep = "AggregatingTickets"`, runs the existing ticket aggregation pass
5. Sets `CurrentStep = "Finalising"`, counts contributors from commit authors, dedupes by lowercased email
6. Finalises the `RepositorySync` row with all counts and contributor JSON

## Non-goals for this feature
- No webhook-driven sync — manual trigger only in v1
- No partial-range sync (always `LatestTag..HEAD`)
- No commit diff content fetching — only metadata (SHA, author, date, message)
- No real-time progress for single-repo standalone syncs — polling at 2s is sufficient at this scale
- No multi-branch sync — default branch only
- No new chart, drawer, or modal on the project screen beyond the small "View run" drawer
- No change to the existing stat cards' styling, position, or labels

## Open questions for `/clarify`

1. **Sync cancellation granularity** — when cancelling a project sync mid-run, should the currently-syncing repo complete or abort? Recommended: complete the current repo (cleaner state, faster code, single transaction commit).
2. **Stale sync detection** — if a sync row is `InProgress` for more than 30 minutes with no heartbeat, should the worker auto-mark it `Failed` on startup? Recommended: yes, with `ErrorMessage = "Stale — worker restarted"`.
3. **Contributor dedup key** — commits sometimes have noreply emails or empty fields. Recommended dedup: `(lowercased email)` with fallback to `(lowercased name)` when email is missing.
4. **Stat card recompute timing** — when a single-repo sync completes, should the four top stat cards recompute immediately (one extra API call) or only on full project sync completion? Recommended: recompute on every successful repo sync — the call is cheap and the user expects fresh totals.

## Estimated effort
- Backend: ~2.5 days (two new tables, two new services, background worker integration, SSE endpoint, snapshot endpoint, parser reuse, tests)
- Frontend: ~2 days (strip component, card state machine, sync button, SSE/polling wiring, contributor popover)
- QA: ~1 day (multi-repo project sync, failure scenarios, cancellation, idempotency, layout regression on existing screen)

Total: roughly 5.5 days for one engineer.
