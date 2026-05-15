# `/plan` Prompt — Latest Tag Selection

> Paste this into Claude Code after `/specify` and `/clarify` are done for this feature branch.

Generate the implementation plan for the Latest Tag Selection feature using the existing project's established patterns. Do NOT introduce new architectural primitives.

## Reuse, don't replace

- Use the existing `IGitProviderService` abstraction in the Infrastructure layer; extend it with `ListTagsAsync`. The Azure DevOps implementation lives in `RepoReleaseManager.Infrastructure.Git.AzureDevOps`.
- Use the existing `RepositoryService` in the Application layer for read/write of the new fields. No MediatR; method on the service.
- Use the existing `RepositoryDto` — add the new fields to it; do not create a parallel DTO.
- Use the existing `[Authorize(Roles = "Admin")]` pattern for write endpoints and `[Authorize]` for the GET.
- Use the existing audit logging helper (`IAuditLogger`) for the set/clear events.

## Backend implementation outline

1. **Domain**
   - Add four properties to `Repository` entity: `LatestTag`, `LatestTagCommitSha`, `LatestTagSetAt`, `LatestTagSetByUserId`
   - Add navigation property `LatestTagSetBy` → `User`
   - Add domain method `Repository.PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)` that enforces `IsTracked == true`
   - Add domain method `Repository.ClearLatestTag(Guid userId, DateTime utcNow)`

2. **Persistence**
   - Add EF configuration mapping the new columns with appropriate max lengths
   - Add migration `AddLatestTagToRepositories`
   - Index on `LatestTag` only if the project screen needs to filter by it; skip otherwise

3. **Provider abstraction**
   - Add `RepositoryTag` record to the Domain layer (it's a value object, not an entity)
   - Add `ListTagsAsync(Guid repositoryId, CancellationToken ct)` to `IGitProviderService`
   - Implement in `AzureDevOpsGitProviderService`:
     - Resolve repository's external ID and parent connection
     - Decrypt PAT, call `GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1`
     - Map response: tag name = ref name without `refs/tags/` prefix; commit SHA = `peeledObjectId` if present else `objectId`; resolve commit date and author via a second call to `GET /_apis/git/repositories/{externalId}/commits/{sha}` only when needed
     - Batch the commit lookups; cap at 200 per fetch
   - Wrap external calls in the existing Polly retry policy

4. **Application service**
   - `RepositoryService.GetTagsAsync(Guid repoId)` — calls provider
   - `RepositoryService.SetLatestTagAsync(Guid repoId, string tagName, Guid userId)` — re-fetches tags, validates tag exists, calls domain method, persists, writes audit entry, commits transaction
   - `RepositoryService.ClearLatestTagAsync(Guid repoId, Guid userId)` — calls domain method, persists, writes audit entry

5. **API**
   - Add three actions to `RepositoriesController`
   - Use `[Authorize(Roles = "Admin")]` on write actions
   - Return `404` if repository not found, `422` if validation fails (not tracked, tag not in remote), `200` on success

## Frontend implementation outline

1. **API client** — add three functions to `repositoriesApi.ts`:
   - `getRepositoryTags(id)`
   - `setLatestTag(id, tagName)`
   - `clearLatestTag(id)`

2. **Repository settings page** — extend the existing `RepositoriesPage`:
   - Make table rows clickable; clicking opens a `Sheet` with `RepositoryDetailPanel`
   - `RepositoryDetailPanel` shows current pinned tag, "Set latest tag" button, "Clear" button (if pinned)
   - "Set latest tag" button opens a `Dialog` with a `DataTable` of tags (powered by `useQuery(['repository', id, 'tags'])`)
   - Sort by date desc by default; search by name
   - Selecting a row + clicking Confirm calls `setLatestTag` mutation; on success invalidates `['repository', id]` and `['project']` query keys, closes the dialog, shows a success toast

3. **Project detail screen** — extend the existing repositories table:
   - Add a "Latest tag" column between Repository and Default branch
   - Render the tag as `<Badge variant="outline" className="font-mono">{tag}</Badge>` or `<span className="text-muted-foreground">—</span>`
   - Wrap in `Tooltip` showing SHA (first 7 chars), date, and "Pinned by {email}"
   - Use an amber dot indicator next to repos with no pinned tag

4. **State management** — no new TanStack Query patterns. Reuse existing keys.

## Testing strategy

- **Domain unit tests**: `Repository.PinLatestTag` rejects when `IsTracked == false`; happy path sets all four fields; `ClearLatestTag` nulls them
- **Infrastructure tests**: `AzureDevOpsGitProviderService.ListTagsAsync` against a WireMock server that mimics the Azure DevOps refs endpoint; cover empty list, paginated list, network error
- **API integration tests**:
  - Admin can fetch, set, and clear
  - Viewer is forbidden on writes (403)
  - Setting a non-existent tag returns 422
  - Setting a tag on an untracked repo returns 422
- **Frontend tests**: component test for the tag-picker dialog; one Playwright E2E for the full Admin flow

## Migration & rollout

- Single migration, run on startup in dev, explicit `dotnet ef database update` in prod
- No data backfill — all existing rows simply get NULL
- No feature flag needed; the feature is additive and read-safe

## Estimated effort

Backend: ~1 day (domain + EF + provider extension + service + controller + tests).
Frontend: ~1 day (detail sheet + tag picker dialog + project column + tests).
QA/Polish: ~half a day.

Total: roughly 2.5 days for one engineer.
