# Tasks — Per-Repo Jira Visibility Feature

A focused milestone breakdown for `/tasks` and `/implement`. Treat this as a single feature delivered in five sub-steps, each independently testable.

---

## Prerequisites

Requires existing milestones M5 (commits + ticket parsing) and M9 (Jira foundation) to be in place. Builds on M10 (release reconciliation) without modifying it.

---

## F1 — Domain primitive + persistence

**Goal:** SemVer value object and snapshot entity in place.

- Add `SemVer` value object with `TryParse` and `NextMinor`
- Add `RepoJiraComparisonSnapshot` entity + EF Core configuration
- Add migration `AddRepoJiraComparisonSnapshot`
- Unit tests covering the spec table (1.30.0 → 1.31.0, 2.5.7 → 2.6.0, 0.9.0 → 0.10.0, plus invalid inputs)

**Smoke test:** `dotnet test` green; migration applies cleanly to a fresh SQLite db.

---

## F2 — Comparison service (backend, no UI)

**Goal:** Compute and persist comparison for a single repo, end to end.

- `IRepoJiraComparisonService` interface in Application
- Implementation in Infrastructure
- Extend `IJiraService` with `GetTicketsInFixVersionByExactNameAsync` if it doesn't already support exact-name lookup
- Cache logic with 5-minute TTL
- Integration test using WireMock for Jira and a fixture commit set: assert correct bucket assignment and match rate

**Smoke test:** call the service from a console test against a real Jira sandbox; verify the snapshot row written and bucket contents.

---

## F3 — API endpoints

**Goal:** Three endpoints wired up with auth.

- `GET /api/v1/repositories/{id}/jira-coverage`
- `GET /api/v1/projects/{id}/jira-coverage`
- `POST /api/v1/repositories/{id}/jira-coverage/add-ticket` (Admin only)
- OpenAPI spec generated
- Integration tests for each endpoint covering happy path, force-refresh, 404, and 403 for non-admin on the POST

**Smoke test:** hit each endpoint via curl with a Viewer and Admin JWT; verify auth behaviour and response shape.

---

## F4 — Service page tab

**Goal:** "Jira coverage" tab fully functional on one repo's detail page.

- `useJiraCoverage` React Query hook
- `RepoCoverageTab` component with header, summary cards, three-bucket breakdown, unmatched commits panel
- `HealthPill` and `BucketList` shared components
- Re-sync button (visible to admin, calls `?refresh=true`)
- "Add to fix version" action on Git-only rows (admin only)
- Skeleton loaders
- Empty states: non-semver tag, fix version missing in Jira, zero commits since tag

**Smoke test:** open a repo with mixed coverage; verify all three buckets render, status pills match Jira, re-sync updates the timestamp, "Add to fix version" round-trips to Jira.

---

## F5 — Project page cards + background refresh

**Goal:** Project page shows per-repo cards; project-wide aggregate header; background refresh keeps caches warm.

- `RepoCoverageCard` component
- `ProjectCoverageAggregate` header
- Card sort toggle (worst-first / alphabetical)
- Wire to `GET /api/v1/projects/{id}/jira-coverage`
- Background `IHostedService` that refreshes snapshots viewed in last 24h, runs every 10 minutes
- `LastViewedAt` column on Repository, updated on each GET

**Smoke test:** open a project with 3+ repos; verify cards render with correct counts, sort order works, deep-link to service page lands on Jira coverage tab; wait 10 min, verify background job refreshed timestamps in the db.

---

## What to validate at the end

End-to-end check across both pages:

1. Latest tag on repo is `1.30.0` → next-version label reads `1.31.0` and Jira fix-version label reads `<RepoName>_1.31.0` on both the project page card and the service page header
2. Create a Jira ticket in the fix version that isn't in any commit → it appears in the "Jira only" bucket on the service page within 5 minutes (or immediately on re-sync)
3. Commit `feat(NEW-999): something` → after commit sync, `NEW-999` appears in "Git only", and clicking "Add to fix version" creates the fix version if absent and adds the ticket
4. Repo has no tags at all → service page shows the "Untagged" indicator; card on the project page shows the same
5. Match rate degrades from 100% → 60%; the card flips from green to amber; project aggregate updates accordingly
