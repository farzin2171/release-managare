# Data Model: Latest Tag Selection for Repositories

**Phase 1 output** — Schema changes for the latest-tag feature. All changes are additive; existing rows get NULL values.

---

## 1. Repositories Table — New Columns

Four columns are appended to the existing `Repositories` table:

| Column | Type | Constraints |
|--------|------|-------------|
| LatestTag | string(255)? | nullable |
| LatestTagCommitSha | string(64)? | nullable |
| LatestTagSetAt | DateTime? | nullable, UTC |
| LatestTagSetByUserId | Guid? | FK → Users.Id, ON DELETE SET NULL, nullable |

**Navigation property**: `LatestTagSetBy → User` (nullable).

**Migration name**: `AddLatestTagToRepositories`. No data backfill required. No index added — the project screen query filters by `ProjectId`, not by `LatestTag`.

---

## 2. RepositoryTag Value Object (not persisted)

```csharp
// Domain/ValueObjects/RepositoryTag.cs
public record RepositoryTag(
    string Name,
    string CommitSha,
    DateTimeOffset CommitDate,
    string AuthorName);
```

Transient value object returned by `IGitProviderService.ListTagsAsync`. Never stored in the database — it represents a tag fetched live from the Git provider and is discarded after the caller selects a tag to pin.

---

## 3. Domain Methods on Repository Entity

Two methods are added to the `Repository` entity in `Domain/Entities/Repository.cs`:

```csharp
public void PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)
{
    if (!IsTracked)
        throw new ValidationException("Repository must be tracked to pin a latest tag.");
    LatestTag = tagName;
    LatestTagCommitSha = commitSha;
    LatestTagSetAt = utcNow;
    LatestTagSetByUserId = userId;
}

public void ClearLatestTag(Guid userId, DateTime utcNow)
{
    LatestTag = null;
    LatestTagCommitSha = null;
    LatestTagSetAt = null;
    LatestTagSetByUserId = null;
}
```

- `PinLatestTag` guards against tagging untracked repositories; all four fields are set atomically.
- `ClearLatestTag` nulls all four fields. The `userId` and `utcNow` parameters are reserved for audit logging at the service layer.

---

## 4. Updated RepositoryDto

The following fields are appended to the existing `RepositoryDto` record in `Application/DTOs/RepositoryDto.cs`:

```csharp
string? LatestTag
string? LatestTagCommitSha
DateTime? LatestTagSetAt
UserSummaryDto? LatestTagSetBy  // { Guid Id, string Email }
```

`UserSummaryDto` is a minimal projection — only `Id` and `Email` — mapped via Mapster from the `LatestTagSetBy` navigation property. When the navigation property is null (tag never pinned, or the user was deleted), `LatestTagSetBy` is null in the response.

---

## 5. EF Core Configuration Note

Add the following Fluent API snippet inside `RepositoryConfiguration` in `Infrastructure/Persistence/Configurations/RepositoryConfiguration.cs`:

```csharp
entity.HasOne(r => r.LatestTagSetBy)
      .WithMany()
      .HasForeignKey(r => r.LatestTagSetByUserId)
      .OnDelete(DeleteBehavior.SetNull);
```

`DeleteBehavior.SetNull` ensures that deleting a user nulls `LatestTagSetByUserId` on any repository they tagged, rather than cascading a delete or raising a constraint violation.
