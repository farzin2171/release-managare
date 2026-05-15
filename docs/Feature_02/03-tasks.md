# `/tasks` Guidance — Latest Tag Selection

> After running `/tasks`, cross-check that the generated `tasks.md` matches the ordering below. Adjust if Spec Kit produces something different.

Ten ordered, individually shippable tasks. Each one ends with a verifiable check.

## T1 — Domain & schema
- Add `LatestTag`, `LatestTagCommitSha`, `LatestTagSetAt`, `LatestTagSetByUserId` to `Repository`
- Add navigation to `User`
- Add `Repository.PinLatestTag` and `Repository.ClearLatestTag` domain methods with unit tests
- EF configuration mapping
- Migration `AddLatestTagToRepositories`
- **Check**: migration applies cleanly to a fresh SQLite DB; domain unit tests green

## T2 — RepositoryTag value object & provider interface
- Add `RepositoryTag` record to Domain
- Add `ListTagsAsync` to `IGitProviderService`
- Stub implementation returning empty list
- **Check**: solution builds; interface is consumed nowhere yet

## T3 — Azure DevOps tag listing
- Implement `ListTagsAsync` in `AzureDevOpsGitProviderService`
- Call refs endpoint, map response, fetch commit metadata in a batched second call
- Wire in Polly retry
- WireMock-based integration test covering happy path, empty list, auth failure
- **Check**: integration test green; manual smoke against a real Azure DevOps repo returns the expected tags

## T4 — Application service methods
- `RepositoryService.GetTagsAsync`
- `RepositoryService.SetLatestTagAsync` — re-fetches, validates, persists, audits
- `RepositoryService.ClearLatestTagAsync` — persists, audits
- Unit tests with mocked provider and DbContext
- **Check**: service unit tests green; audit entries written for set/clear

## T5 — API endpoints
- `GET /api/v1/repositories/{id}/tags`
- `PUT /api/v1/repositories/{id}/latest-tag`
- `DELETE /api/v1/repositories/{id}/latest-tag`
- Extend `RepositoryDto` and the existing `GET /api/v1/repositories/{id}` response
- Authorize attributes
- API integration tests covering Admin, Viewer, untracked repo, invalid tag
- **Check**: tests green; OpenAPI document regenerated and reviewed

## T6 — Frontend API client
- Three functions in `repositoriesApi.ts`
- TanStack Query hooks: `useRepositoryTags`, `useSetLatestTag`, `useClearLatestTag`
- **Check**: TypeScript compiles; hooks tested with MSW handlers

## T7 — Repository detail Sheet
- New `RepositoryDetailPanel` component
- Wired to existing repositories page
- Shows pinned tag, set/clear buttons, "Last set by" line
- Role-gated buttons (hidden for Viewer)
- **Check**: opens from the repositories table; matches design

## T8 — Tag picker Dialog
- New `TagPickerDialog` component using shadcn `Dialog` + `DataTable`
- Sortable by date, searchable by name
- Calls `useSetLatestTag` on confirm
- Invalidates `['repository', id]` and `['project']` keys on success
- Error toast + Retry on failed fetch
- Warning banner if pinned tag no longer in remote list
- **Check**: E2E happy-path test green

## T9 — Project screen integration
- Add "Latest tag" column to the project detail repositories table
- `Badge` + `Tooltip` rendering with SHA, date, pinned-by
- Amber-dot indicator for repos with no pinned tag
- **Check**: visual review matches the spec; column sorts correctly

## T10 — Polish & docs
- README section on the latest-tag concept and why it matters for release notes
- Audit log review: confirm every set/clear is captured
- Manually verify drift detection banner (pin tag, delete tag in Azure DevOps, refresh)
- **Check**: full Admin → Viewer walk-through passes
