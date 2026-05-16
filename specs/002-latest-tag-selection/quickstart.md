# Quickstart: Latest Tag Selection Feature

Developer notes for implementing and testing the `002-latest-tag-selection` feature on top of the existing platform.

---

## Prerequisites

This feature builds on a running instance of the base platform (feature 001). Before starting:

1. The main solution builds: `dotnet build backend/src`
2. The database exists with baseline migrations applied
3. At least one `GitProviderConnection` with a valid PAT and at least one tracked `Repository` exist in the database

---

## Step 1: Apply the Migration

```powershell
dotnet ef migrations add AddLatestTagToRepositories `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api

dotnet ef database update `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

All existing repository rows will have `NULL` values for the four new columns. No backfill is needed.

---

## Step 2: Run Domain Unit Tests First (TDD)

Write the tests in `RepoManager.UnitTests/Domain/RepositoryLatestTagTests.cs` **before** implementing the domain methods. The tests must fail initially (Red), then pass after implementation (Green).

Required test cases:
- `PinLatestTag` on an untracked repository throws `ValidationException`
- `PinLatestTag` on a tracked repository sets all four fields correctly
- `ClearLatestTag` sets all four fields to null

```powershell
dotnet test backend/tests/RepoManager.UnitTests `
  --filter "FullyQualifiedName~RepositoryLatestTagTests"
```

---

## Step 3: Implement Backend (in order)

1. `RepositoryTag.cs` value object in `RepoManager.Domain/ValueObjects/`
2. `Repository.PinLatestTag` and `Repository.ClearLatestTag` domain methods
3. `IGitProviderService.ListTagsAsync` interface extension
4. `AzureDevOpsGitProvider.ListTagsAsync` implementation
5. `IRepositoryService` extensions + `RepositoryDto` new fields
6. `RepositoryService` implementations (GetTagsAsync, SetLatestTagAsync, ClearLatestTagAsync)
7. `RepositoryConfiguration.cs` EF Core column and FK mappings
8. Three new actions on `RepositoriesController`

---

## Step 4: Regenerate Frontend API Client

After the backend endpoints are live:

```powershell
cd frontend
npm run codegen   # fetches http://localhost:5000/swagger/v1/swagger.json
```

Verify `src/lib/api.d.ts` includes the new tag endpoints and the updated `RepositoryDto` fields.

---

## Step 5: Implement Frontend (in order)

1. Add `getRepositoryTags`, `setLatestTag`, `clearLatestTag` to `repositoriesApi.ts`
2. Build `RepositoryDetailSheet.tsx` — shows current pinned tag, "Fetch tags" button, "Clear" button
3. Build `TagPickerDialog.tsx` — `DataTable` of tags, sort by date desc, search by name, Confirm button
4. Wire the sheet into the existing `RepositoriesPage` (make table rows clickable)
5. Modify `ProjectRepositoriesTable.tsx` — add "Latest tag" column with `Badge`, `Tooltip`, amber dot

---

## Step 6: Verify End-to-End

Manual smoke test:

1. Log in as Admin → Settings → Repositories
2. Click a tracked repository row → sheet opens
3. Click "Fetch tags" → loading state → tag list appears
4. Select a tag → Confirm → success toast → panel shows new pinned tag
5. Open a project → project detail screen → "Latest tag" column shows the badge
6. Hover badge → tooltip shows SHA, date, email
7. Return to settings → Clear tag → confirmation dialog → cleared → project screen shows "—" with amber dot
8. Log in as Viewer → Settings → Repositories → open panel → Fetch/Set/Clear buttons not present

---

## Key Implementation Notes

**Azure DevOps refs endpoint**

```
GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1
```

- `peeledObjectId` = commit SHA for annotated tags; fall back to `objectId` for lightweight tags
- Batch commit-detail lookups via `Task.WhenAll` (up to 200 tags per fetch)

**Write-time validation**

`SetLatestTagAsync` re-fetches the live tag list before persisting. If the tag name is not in the fresh list, return `422 Unprocessable Entity` with a `ProblemDetails` body.

**Audit logging**

```csharp
await _auditLogger.LogAsync(AuditAction.LatestTagSet, new {
    repositoryId,
    oldValue = existing.LatestTag,
    newValue = tagName,
    actingUserId
}, ct);
```

**TanStack Query invalidation on success**

```typescript
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: ['repository', repositoryId] });
  queryClient.invalidateQueries({ queryKey: ['project', projectId] });
}
```
