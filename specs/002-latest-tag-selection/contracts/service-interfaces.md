# Service Interfaces Contract: Latest Tag Selection

**Phase 1 output** — Interface extensions and new method signatures for the latest-tag feature.

---

## IGitProvider — extended method

`ListTagsAsync` is already declared on the base `IGitProvider` interface (see `specs/001-release-mgmt-platform/contracts/service-interfaces.md`). No interface change is required; the existing signature is used as-is:

```csharp
// Application/GitProviders/IGitProvider.cs (existing — no change)
Task<IEnumerable<TagInfo>> ListTagsAsync(
    ProviderConnection conn,
    string repoExternalId,
    CancellationToken ct = default);

// Existing DTO — AuthorName field added for this feature
record TagInfo(string Name, string CommitSha, DateTimeOffset? CommitDate, string? AuthorName);
```

**Azure DevOps implementation** (`AzureDevOpsGitProvider`): calls
`GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1`.
The `peelTags=true` parameter resolves annotated tag objects to their underlying commit SHA and date.

---

## Domain value object — RepositoryTag

```csharp
// Domain/Repositories/RepositoryTag.cs
public sealed record RepositoryTag(
    string Name,
    string CommitSha,
    DateTimeOffset? CommitDate,
    string? AuthorName
);
```

Returned by service methods; mapped from `TagInfo` (provider layer) by `IRepositoryService` implementations. The `TagInfo` → `RepositoryTag` mapping is one-to-one; the rename reinforces the domain boundary.

---

## IRepositoryService — new methods

```csharp
// Application/Repositories/IRepositoryService.cs (extensions)

// Returns the live tag list by delegating to IGitProvider.ListTagsAsync.
// Throws NotFoundException if repositoryId is unknown.
// Throws ValidationException if the repository is not tracked.
Task<IReadOnlyList<RepositoryTag>> GetTagsAsync(
    Guid repositoryId,
    CancellationToken ct = default);

// Re-fetches tags from the provider, validates that tagName exists,
// calls Repository.PinLatestTag(tag, actingUserId), persists, writes audit entry.
// Throws NotFoundException if repositoryId is unknown.
// Throws ValidationException if repository is not tracked or tag is not in remote.
Task<RepositoryDto> SetLatestTagAsync(
    Guid repositoryId,
    string tagName,
    Guid actingUserId,
    CancellationToken ct = default);

// Calls Repository.ClearLatestTag(actingUserId), persists, writes audit entry.
// Idempotent — succeeds even if no tag is currently pinned.
// Throws NotFoundException if repositoryId is unknown.
Task ClearLatestTagAsync(
    Guid repositoryId,
    Guid actingUserId,
    CancellationToken ct = default);
```

---

## RepositoryDto — new fields

```csharp
// Application/Repositories/RepositoryDto.cs (additions)
public sealed record RepositoryDto(
    // ... existing fields unchanged ...
    string? LatestTag,
    string? LatestTagCommitSha,
    DateTimeOffset? LatestTagSetAt,
    UserSummaryDto? LatestTagSetBy        // null if no tag pinned or user not resolvable
);

record UserSummaryDto(Guid Id, string Email);
```

---

## Domain entity — Repository aggregate extensions

```csharp
// Domain/Repositories/Repository.cs (additions — domain behaviour, not interface)

// Stores pinned tag state and validates business rules.
public void PinLatestTag(RepositoryTag tag, Guid actingUserId);
// Clears pinned tag state.
public void ClearLatestTag(Guid actingUserId);
```

These methods are called exclusively by `IRepositoryService` implementations; controllers never touch the entity directly.

---

## EF Core — new columns

The following columns are added to the `Repositories` table via a new migration:

| Column | Type | Nullable |
|--------|------|----------|
| `LatestTag` | `TEXT` | Yes |
| `LatestTagCommitSha` | `TEXT` | Yes |
| `LatestTagSetAt` | `TEXT` (ISO 8601) | Yes |
| `LatestTagSetById` | `TEXT` (FK → Users) | Yes |

Migration name: `AddRepositoryLatestTag`.

---

## TanStack Query Keys (Frontend)

```typescript
// Query key conventions — feature 002 additions:
['repository', id]              // repository detail; invalidated on set/clear tag
['repository', id, 'tags']      // live tag list; staleTime: 0 (always re-fetched, never cached)
['project', projectId]          // invalidated on set/clear (project screen shows latest-tag per repo)
```

Invalidation sequence on `PUT /repositories/{id}/latest-tag` or `DELETE /repositories/{id}/latest-tag`:
1. Invalidate `['repository', id]` — refreshes detail panel.
2. Invalidate `['project', projectId]` — refreshes parent project screen if open.

The `['repository', id, 'tags']` key is never manually invalidated; the tag picker always issues a fresh fetch on open.

---

## Validation rules

| Rule | Enforced by |
|------|-------------|
| `tagName` must be non-empty and ≤ 250 characters | FluentValidation on `SetLatestTagDto` |
| Repository must have `isTracked = true` | `IRepositoryService.SetLatestTagAsync` / `GetTagsAsync` |
| Tag must exist in the remote provider | `IRepositoryService.SetLatestTagAsync` (re-fetch + set-membership check) |

---

All service methods follow project-wide conventions:
- `CancellationToken ct = default` as last parameter
- Accept and return DTOs (record types), never EF entities
- Write DTOs validated with `await _validator.ValidateAndThrowAsync(dto, ct)` before processing
- Typed exceptions only: `NotFoundException`, `ValidationException`, `ExternalServiceException`
