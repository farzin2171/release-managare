# Specify Addendum — Per-Repo Release Versioning

> Append the following capability to the existing spec. Do not regenerate or modify other sections.

## Context

Today a release on the Project page is created with a **single version** that applies to the whole project, derived from the primary repo's latest semver tag. Teams have raised that real-world releases ship different repos at different cadences — the API repo bumps minor, the web repo bumps patch, a shared library may not move at all.

This feature changes how a release is **composed** without touching projects, repos, or any settings page:

- At release-creation time, the user selects **which of the project's tracked repos** are included in the release.
- The user sets a **distinct next version per included repo**.
- The release-level version label is **derived from the primary repo's new version**, not entered by the user.
- The project page gains a **Releases list** showing all releases created for that project.

## User Stories

**As a tech lead creating a release**, I want to include only the repos that have changes this cycle and set each one's next version independently, so the release reflects what actually ships.

**As a tech lead viewing a project**, I want to see all releases ever created for it (name, label, date, repo count, status) so I can audit history without leaving the project page.

**As an admin reviewing a published release**, I want each included repo to show its previous version, new version, and bump type, captured at the moment the release was created, so the record stays accurate even if repo tags drift later.

## Functional Requirements

### FR-1 — Release composition

- Repos available for inclusion in a release are exactly the repos assigned to the project (via the existing `ProjectRepository` join). No new repo-picking surface is introduced.
- A repo with no commits since its last tag is shown in the wizard but **not pre-selected**. The user may still include it (e.g., to publish a version bump without code changes — rare but valid).
- A repo with commits since its last tag is **pre-selected** by default.
- At least one repo must be selected to proceed.

### FR-2 — Per-repo version suggestion

- For each selected repo, the system suggests a next version from the repo's latest semver tag plus the dominant change type in the commit range:
  - any commit with `BREAKING CHANGE` footer or `!` after type → `major`
  - else any `feat` commit → `minor`
  - else (`fix`, `chore`, `refactor`, `docs`, etc.) → `patch`
- The suggested next version and bump type are pre-filled but **editable**.
- Bump type radios (`major` / `minor` / `patch` / `custom`) drive the next-version field:
  - selecting `major`/`minor`/`patch` recomputes the field from the previous tag.
  - editing the next-version field manually flips the radio to `custom`.
- Validation: the next version must be a valid semver string and **strictly greater than** the previous version. Submission is blocked otherwise.

### FR-3 — Derived release label

- The release's overall version label (`Release.Version`) is set automatically to the new version of the project's **primary repo**, if the primary repo is included in the release.
- If the primary repo is **not** included, the release label falls back to the new version of the first included repo when sorted alphabetically by repo name. The UI must show which repo the label is derived from.
- The label is **not directly editable** in the wizard; it updates live as the user changes the primary repo's next version.

### FR-4 — Historical snapshot

- For each included repo, the release stores at the moment of creation:
  - `PreviousVersion` (the tag the repo was at)
  - `NextVersion` (the version the user confirmed)
  - `BumpType` (`major` / `minor` / `patch` / `manual`)
  - `FromCommitSha` (the commit at `PreviousVersion`)
  - `ToCommitSha` (the HEAD on default branch at creation time)
  - `CommitCount` and `TicketCount` covering that range
- These values are **immutable** once the release is created. They are not re-derived on later views — even if the underlying tag is deleted, branch is force-pushed, or repo is removed from the project.

### FR-5 — Releases list on the Project page

- The Project page gains a **Releases** section (tab or panel — implementation-level choice) listing all releases for that project.
- Columns: **Name**, **Version (label)**, **Date created**, **Status** (`Draft` / `Published` / `Archived`), **Repo count**.
- Default sort: most recently created first.
- Filter: by status. Search: by name.
- Each row is clickable and opens the existing Release detail page, which must now render the per-repo table (previous → next, bump type, counts) from the stored snapshot.

### FR-6 — Edit rules

- While a release is in `Draft` status, the user can change repo selection and per-repo next versions. All snapshot fields (`FromCommitSha`, counts, etc.) are re-captured on save.
- Once a release is `Published`, the repo selection and per-repo versions are **frozen**. Only `NotesMarkdown` and `ConfluencePageUrl` remain editable. Existing edit-permissions rules apply unchanged.

## Permissions

- **Admin**: create, edit (while Draft), publish, archive releases.
- **Viewer**: read-only access to the Releases list and Release detail page.

## Non-Goals (explicit)

- No cross-project releases (a release spanning more than one logical project).
- No editing of repo→project assignment from inside the wizard. Stays in Settings → Projects.
- No automatic creation of Git tags. Pushing the tag remains part of the existing publish flow and is unchanged by this feature.
- No support for non-semver versioning schemes (CalVer, date-based). The existing semver-only constraint stays.
- No bulk operations on the releases list (delete multiple, export, etc.) in v1.

## Clarifying Questions (likely to surface in `/clarify`)

1. **Primary repo not included** — confirm fallback to alphabetical-first included repo for the release label (spec assumes yes). Alternatives: block submission, or prompt for a temporary primary.
2. **Backfill of legacy releases** — confirm that existing releases backfill to a single `ReleaseRepository` row pointing at the primary repo, with empty snapshot fields acceptable for legacy rows.
3. **Reconciliation impact** — confirm that updating the existing Jira reconciliation feature to iterate over `Release.ReleaseRepositories` (instead of `Project.Repositories`) is a **follow-up task**, not in scope here.
