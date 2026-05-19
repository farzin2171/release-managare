# Data Model: Per-Repo Jira Coverage

**Phase 1 output** — New entity, value object, and entity extensions for the repo-jira-coverage feature.

---

## 1. New Entity: `RepoJiraComparisonSnapshot`

One row per `(RepositoryId, NextVersion)` pair. Created on first computation; replaced atomically on each re-sync. The unique constraint on `(RepositoryId, NextVersion)` ensures only one snapshot exists per repo+version combination.

### Table: `RepoJiraComparisonSnapshots`

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `int` | PK, auto-increment |
| `RepositoryId` | `int` | FK → `Repositories.Id`, ON DELETE CASCADE, NOT NULL |
| `CurrentTag` | `string(64)` | NOT NULL, `""` when untagged |
| `NextVersion` | `string(32)` | NOT NULL (e.g., `1.31.0` or `0.1.0`) |
| `JiraFixVersionName` | `string(128)` | NOT NULL (e.g., `Services.UX_1.31.0`) |
| `JiraFixVersionExists` | `bool` | NOT NULL |
| `CommitCount` | `int` | NOT NULL |
| `GitTicketCount` | `int` | NOT NULL |
| `JiraTicketCount` | `int` | NOT NULL |
| `InBothCount` | `int` | NOT NULL |
| `JiraOnlyCount` | `int` | NOT NULL |
| `GitOnlyCount` | `int` | NOT NULL |
| `MatchRate` | `decimal(5,4)` | NOT NULL, 0.0000–1.0000 |
| `Supported` | `bool` | NOT NULL; `false` when non-semver tag |
| `UnsupportedReason` | `string(256)?` | nullable |
| `InBothJson` | `string` (TEXT) | NOT NULL, default `[]` |
| `JiraOnlyJson` | `string` (TEXT) | NOT NULL, default `[]` |
| `GitOnlyJson` | `string` (TEXT) | NOT NULL, default `[]` |
| `UnmatchedCommitsJson` | `string` (TEXT) | NOT NULL, default `[]` |
| `LastSyncedAt` | `DateTime` | NOT NULL, UTC; set to `DateTime.MinValue` on invalidation |
| `LastSyncError` | `string(1024)?` | nullable; last error message if sync failed |

**Index**: `IX_RepoJiraComparisonSnapshots_RepositoryId_NextVersion` — unique on `(RepositoryId, NextVersion)`.

**Migration name**: `AddRepoJiraComparisonSnapshot`.

### EF Core Configuration (`RepoJiraComparisonSnapshotConfiguration.cs`)

```csharp
entity.HasKey(s => s.Id);
entity.HasIndex(s => new { s.RepositoryId, s.NextVersion }).IsUnique();
entity.HasOne(s => s.Repository)
      .WithMany(r => r.JiraComparisonSnapshots)
      .HasForeignKey(s => s.RepositoryId)
      .OnDelete(DeleteBehavior.Cascade);
entity.Property(s => s.MatchRate).HasPrecision(5, 4);
entity.Property(s => s.InBothJson).HasDefaultValue("[]");
entity.Property(s => s.JiraOnlyJson).HasDefaultValue("[]");
entity.Property(s => s.GitOnlyJson).HasDefaultValue("[]");
entity.Property(s => s.UnmatchedCommitsJson).HasDefaultValue("[]");
```

---

## 2. Entity Extension: `Repository` — `LastViewedAt` Column

One nullable `DateTime?` column added to the existing `Repositories` table. Updated every time `GetForRepoAsync` is called. Used by the background refresh job to scope its work to repos viewed in the last 24 hours.

| Column | Type | Constraints |
|--------|------|-------------|
| `LastViewedAt` | `DateTime?` | nullable, UTC |

**Migration**: Included in the same `AddRepoJiraComparisonSnapshot` migration as the new table. No data backfill; existing rows default to `NULL`.

**Navigation property**: `Repository.JiraComparisonSnapshots` — `ICollection<RepoJiraComparisonSnapshot>` (added for EF navigation; not returned in DTOs).

---

## 3. Domain Value Object: `SemVer`

Located at `RepoManager.Domain/ValueObjects/SemVer.cs`.

```csharp
public sealed record SemVer(int Major, int Minor, int Patch)
{
    public static bool TryParse(string tag, out SemVer? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(tag)) return false;

        var s = tag.StartsWith('v') ? tag[1..] : tag;
        var parts = s.Split('.');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0], out var major)) return false;
        if (!int.TryParse(parts[1], out var minor)) return false;
        if (!int.TryParse(parts[2], out var patch)) return false;

        result = new SemVer(major, minor, patch);
        return true;
    }

    public SemVer NextMinor() => new(Major, Minor + 1, 0);

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
```

**Not persisted** — computed on demand by `RepoJiraComparisonService`. Unit-tested with the table from the spec (see Decision 2 in `research.md`).

---

## 4. `HealthBand` Enumeration

Located at `RepoManager.Domain/Enums/HealthBand.cs`.

```csharp
public enum HealthBand
{
    Green,    // MatchRate >= 0.90
    Amber,    // MatchRate >= 0.60 && < 0.90
    Red,      // MatchRate < 0.60
    Unknown   // Comparison not supported (non-semver tag, etc.)
}
```

---

## 5. New DTO Types (Application Layer)

Located in `RepoManager.Application/Jira/Dtos/`.

### `RepoJiraComparisonDto`

```csharp
public record RepoJiraComparisonDto(
    int RepositoryId,
    string RepositoryName,
    string? CurrentTag,
    string? NextVersion,
    string? JiraFixVersionName,
    bool JiraFixVersionExists,
    bool Supported,
    string? UnsupportedReason,
    ComparisonCounts Counts,
    decimal MatchRate,
    HealthBand Health,
    IReadOnlyList<TicketSummaryDto> InBoth,
    IReadOnlyList<TicketSummaryDto> JiraOnly,
    IReadOnlyList<TicketSummaryDto> GitOnly,
    IReadOnlyList<CommitSummaryDto> UnmatchedCommits,
    DateTime LastSyncedAt
);
```

### `ComparisonCounts`

```csharp
public record ComparisonCounts(
    int CommitCount,
    int GitTicketCount,
    int JiraTicketCount,
    int InBothCount,
    int JiraOnlyCount,
    int GitOnlyCount
);
```

### `TicketSummaryDto`

```csharp
public record TicketSummaryDto(
    string Key,
    string? Summary,
    string? Status,
    string? StatusCategory,   // "To Do" / "In Progress" / "Done"
    string? AssigneeAvatarUrl,
    int CommitCount           // 0 for Jira-only tickets
);
```

### `CommitSummaryDto`

```csharp
public record CommitSummaryDto(
    string Sha,
    string AuthorName,
    string Message
);
```

### `ProjectJiraCoverageDto`

```csharp
public record ProjectJiraCoverageDto(
    int ProjectId,
    string ProjectName,
    int TotalRepoCount,
    int GreenRepoCount,
    int AttentionRepoCount,    // Amber + Red
    decimal ProjectMatchRate,  // Weighted by ticket count
    IReadOnlyList<RepoJiraComparisonDto> Repos
);
```

### `AddToFixVersionResultDto`

```csharp
public record AddToFixVersionResultDto(
    bool Success,
    string JiraFixVersionName,
    bool FixVersionCreated    // true if the fix version was newly created in Jira
);
```
