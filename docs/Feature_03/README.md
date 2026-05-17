# Project Screen Sync — Spec Kit Feature Bundle

A paste-ready spec for adding **repository sync** and **project-wide sync** to the existing project detail screen. Designed to slot into the current card-grid UX with **three additive elements** — no layout restructuring, no removed components, no recoloured chrome.

## What this feature adds

- A "Sync" button in the footer of every repository card. Triggers a sync that fetches commits between the pinned latest tag and HEAD, parses them with the existing conventional-commits parser, extracts Jira tickets and contributors, and **persists everything to a new `RepositorySyncs` table**.
- A "Sync project" action in a new thin strip between the title row and the existing "UNRELEASED CHANGES" stat cards — syncs every assigned repo one-by-one with live progress reflected directly on each repo card (no separate progress panel).
- Two new tables: `RepositorySyncs` and `ProjectSyncs`. Everything else is reused: `Commits`, `Tickets`, `IConventionalCommitParser`, `IGitProviderService`.
- On screen reload, the existing four stat cards at the top and the four metrics inside each repo card render from the database — no Git provider calls in the read path.

## What this feature deliberately does NOT change

- The existing page header, breadcrumb, project title row, and "New release" button — untouched
- The existing "UNRELEASED CHANGES ACROSS ALL REPOSITORIES" section with its four stat cards — untouched (they just start showing real numbers)
- The existing two-column repo card grid layout — untouched
- The existing four-metric grid inside each repo card (commits / tickets / breaking / contributors) — untouched
- All existing component file structure — three minimal edits to two files; everything else is new components added alongside

## The three new UI elements

1. **"Project last synced" strip** — sits between the project title row and the existing stat cards. Shows the timestamp, sync summary, "View run" link, and the "Sync project" button. Transforms to a live progress indicator during a project sync.
2. **Tag chip on each card** — replaces the existing `→ HEAD` text in the top-right of each card with a `v2.4.1 → HEAD` pill. Falls back to `No tag → HEAD` with an amber dot when nothing is pinned.
3. **Footer strip on each card** — small section below the existing four-metric grid showing the last-synced timestamp on the left and a "Sync" button on the right. Adds ~30px to card height.

Plus three transient card states (in-progress / failed / no-pinned-tag) that **temporarily replace** the four-metric grid with a centered status block, then revert when the sync completes.

## Non-breaking guarantee

- No existing column on `Repositories`, `Projects`, `Commits`, `Tickets` is modified
- No existing API endpoint changes shape
- No existing component is removed, repurposed, or restyled
- Two new tables, eight new endpoints, five new components, two existing files lightly edited

## Files in this bundle

| Order | Command | File | What it does |
|-------|---------|------|--------------|
| 1 | `/specify` | `01-specify.md` | Four user stories, three-element UI design, data model, API surface, non-goals, open questions |
| 2 | `/clarify` | (interactive) | Answers the four clarification questions at the bottom of `01-specify.md` |
| 3 | `/plan` | `02-plan.md` | File-by-file backend + frontend implementation, reusing existing patterns, with minimal touch points on existing components |
| 4 | `/tasks` | `03-tasks.md` | Twelve ordered, individually shippable tasks |
| 5 | `/analyze` | — | Standard consistency check |
| 6 | `/implement` | — | Run T1 first, verify, then proceed task-by-task |

## How to run it

```bash
# from the root of your existing repo-release-manager project
git checkout -b feature/project-sync

# in Claude Code:
/specify   # paste 01-specify.md
/clarify   # answer the four open questions
/plan      # paste 02-plan.md
/tasks     # then compare against 03-tasks.md
/analyze
/implement # T1 first, then T2, etc — review after each
```

## Key design decisions worth highlighting

1. **The card is the progress indicator** — no separate progress panel, no layout shift during sync. The currently-syncing repo's card tints blue and shows a live status line where its metric grid usually sits; completed repos show their new metric values; pending repos look idle. Means zero structural change to the existing screen.

2. **Persistence-first reads** — after the first sync, the project screen reads counts from `RepositorySyncs` and the existing `Commits` / `Tickets` rows via a single `/repositories/sync-snapshot` endpoint. No Git provider call on screen load.

3. **Sequential project sync** with **SSE progress streaming** — one repo at a time, ordered as listed. SSE keeps the implementation dependency-free (no SignalR) and matches the existing .NET stack.

4. **Idempotency** via the natural key `(RepositoryId, Sha)` on `Commits` — re-running a sync is safe and cheap. Existing `Tickets` aggregation is already idempotent.

5. **One active project sync at a time** — enforced by a unique partial index on `ProjectSyncs.Status` for `Pending`/`InProgress` per `ProjectId`. Second concurrent attempt returns 409.

6. **Viewer can sync** — sync is read-replication of upstream Git data, not a privileged write. The PAT-bearing connection is already Admin-gated upstream.

7. **5,000-commit cap per sync** — guardrail against runaway runs on neglected repos. Fails loudly with a "pin a more recent tag" message.

## What's intentionally out of scope for this feature

- No webhook-driven background sync — manual trigger only in v1
- No multi-branch sync — default branch only
- No arbitrary range syncs — always `LatestTag..HEAD`
- No commit diff content — only metadata
- No new chart, drawer, or modal beyond the small "View run" drawer
- No real-time progress for single-repo standalone syncs (polling is fine)

These all stay deferred to v2 to keep the surface area focused.
