# Feature: Latest Tag Selection for Repositories

> Paste this content into Claude Code after running `/specify` for a new feature branch.
> Spec Kit will create a new `specs/NNN-latest-tag/spec.md` file from this input.

## Feature Summary

Add the ability for an Admin to view all available Git tags for a tracked repository and explicitly pin one of them as the repository's "latest tag". The pinned latest tag is persisted on the `Repositories` table and surfaced on the project screen so the team can see, at a glance, which tag is considered the current baseline for each repo in a project.

This feature is the foundation for the project-level "changes since last tag" view: when a tech lead opens a project, the system must already know which tag each assigned repo is anchored to, rather than guessing the latest semver tag at query time.

## User Stories

### Story 1 — Admin pins the latest tag for a repository
**As an** Admin
**I want to** open a tracked repository's settings, see every tag that exists in the remote repo, and select one as the "latest tag"
**So that** the project screen and future release flows have a deterministic baseline to compare against.

**Acceptance criteria**
- From `Settings → Repositories`, clicking a row opens a repository detail panel (Sheet or drawer)
- The panel has a "Latest tag" section showing the currently pinned tag (or "Not set" if none)
- A "Fetch tags" action calls the Git provider, lists every tag in the repo with: tag name, commit SHA (short), commit date, author name
- Tags are sortable by date (desc by default) and filterable by name
- Selecting a tag and confirming saves it to the repository row as `LatestTag` and `LatestTagSetAt`
- After save, the panel shows the new pinned tag with a "Last set: X ago by Y" line
- Admin can clear the pinned tag (sets `LatestTag` to null)
- All write actions require the Admin role; Viewer sees the pinned value read-only and no Fetch/Select buttons

### Story 2 — Viewer sees the latest tag on the project screen
**As a** Viewer or Admin
**I want to** see each repository's pinned latest tag on the project detail screen
**So that** I understand the baseline the team is working from without opening repository settings.

**Acceptance criteria**
- The project detail screen's repositories table gains a "Latest tag" column
- The column shows the pinned tag name as a monospaced badge, or "—" if not set
- Hovering the badge shows a tooltip with the commit SHA, date, and who pinned it
- An unset latest tag is visually flagged (amber dot or muted text) because it means the project is not fully configured
- The column is sortable and the value is included when the project screen's data is exported (if export exists)

### Story 3 — Tag list stays fresh
**As an** Admin
**I want** the "Fetch tags" action to always hit the Git provider directly
**So that** I never pin a stale tag that has since been replaced or deleted on the remote.

**Acceptance criteria**
- Fetch tags is on-demand only; the system does NOT cache the tag list between sessions
- Loading state is shown while the provider call is in flight
- Provider errors (auth failure, network timeout, repo not found) surface as a clear error toast with a Retry button
- If the previously pinned tag no longer exists on the remote, the panel shows a warning banner: "The pinned tag `vX.Y.Z` no longer exists in the remote repository. Pick a new one."

## Functional Requirements

1. **Schema change** — add to `Repositories`:
   - `LatestTag` (string, nullable, max 255)
   - `LatestTagCommitSha` (string, nullable, max 64) — the SHA the tag points at when it was pinned, used to detect drift
   - `LatestTagSetAt` (DateTime, nullable, UTC)
   - `LatestTagSetByUserId` (Guid, nullable, FK → Users.Id, ON DELETE SET NULL)

2. **EF Core migration** — name it `AddLatestTagToRepositories`. No data backfill; existing rows get NULL values.

3. **Git provider abstraction** — extend the existing `IGitProviderService` (or equivalent) with:
   ```csharp
   Task<IReadOnlyList<RepositoryTag>> ListTagsAsync(
       Guid repositoryId,
       CancellationToken ct);
   ```
   where `RepositoryTag` is `{ Name, CommitSha, CommitDate, AuthorName }`. The Azure DevOps implementation maps to `GET /_apis/git/repositories/{repoId}/refs?filter=tags&peelTags=true`.

4. **API endpoints** (versioned under `/api/v1`):
   - `GET    /api/v1/repositories/{id}/tags` — returns the live tag list from the provider. Admin only.
   - `PUT    /api/v1/repositories/{id}/latest-tag` — body `{ tagName: string }`. Admin only. Server re-fetches tags to validate the tag still exists and to capture the current SHA before persisting.
   - `DELETE /api/v1/repositories/{id}/latest-tag` — clears the pin. Admin only.
   - `GET    /api/v1/repositories/{id}` — existing endpoint must now include `latestTag`, `latestTagCommitSha`, `latestTagSetAt`, `latestTagSetBy { id, email }` in the response DTO.

5. **Validation rules**
   - Repository must be `IsTracked = true` to set a latest tag (returns 422 otherwise)
   - Tag name must be present in the live tag list at write time
   - Concurrent writes use last-write-wins, but `LatestTagSetAt` is server-generated

6. **Audit logging** — every write to `LatestTag` (set or clear) logs an audit entry with user ID, repository ID, old value, new value, and timestamp. Reuse the existing audit pattern; do not introduce a new logging primitive.

7. **Frontend** — implement with existing shadcn components:
   - `Sheet` for the repository detail panel
   - `DataTable` for the tag list
   - `Badge` + `Tooltip` for the project screen column
   - `AlertDialog` for the "Clear pinned tag" confirmation
   - TanStack Query keys: `['repository', id]`, `['repository', id, 'tags']` — invalidate `['repository', id]` and `['project', projectId]` on successful write

## Non-Functional Requirements

- **Performance**: `ListTagsAsync` must paginate the provider response if it exceeds 500 tags; default page size 200. The full list is returned to the frontend (no server-side pagination in v1) but the table must virtualize when over 100 rows.
- **Security**: Personal Access Token used for the Git call is the one stored on the parent `GitProviderConnection`; never exposed to the client.
- **Observability**: Each tag fetch logs `repository.tags.fetched` with duration, tag count, and outcome. Each set/clear logs `repository.latest_tag.changed` with old → new.
- **Backwards compatibility**: `LatestTag` is nullable; nothing else in the system requires it to be set. The release workflow continues to use semver inference if `LatestTag` is null (this preserves existing behaviour for repos not yet pinned).

## Out of Scope (v1 of this feature)

- Automatic detection of "new tag available since pin" — that's a follow-up notification feature
- Bulk pinning across multiple repositories
- Per-project tag overrides (the latest tag is currently a repository-level property, not a project-repository assignment property — revisit if teams ask for it)
- Webhooks from Azure DevOps to auto-update when a new tag is pushed

## Open Questions for `/clarify`

1. Should the project screen highlight repositories where the pinned tag's SHA no longer matches the remote (i.e., the tag was force-moved)? If yes, that needs a background refresh job — significantly larger scope.
2. When a repository is untracked (`IsTracked` set to false) should the pinned tag be cleared automatically, or preserved in case it's re-tracked later?
3. Are non-annotated (lightweight) tags shown alongside annotated tags, or filtered out? Recommendation: show both, but mark lightweight tags with a small icon.
