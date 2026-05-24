# Data Model: Per-Repo Release Versioning

**Phase**: 1 — Design  
**Branch**: `006-per-repo-release-versioning`  
**Date**: 2026-05-23

## Entity Changes

### Modified: `Release`

**Change**: Add navigation property `ICollection<ReleaseRepository> ReleaseRepositories`.  
`Version` semantics shift from "user-entered" to "derived from primary repo's `NextVersion` at save time", but the column definition is unchanged.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `int` PK | Existing |
| `ProjectId` | `int` FK → `Projects` | Existing |
| `Name` | `nvarchar` | Existing |
| `Version` | `nvarchar` | Existing — now derived, not user-entered |
| `Status` | `int` (enum) | Existing — `Draft=0`, `Published=1`, `Archived=2` |
| `CreatedAt` | `datetime` | Existing |
| `PublishedAt` | `datetime?` | Existing |
| `ConfluencePageUrl` | `nvarchar?` | Existing |
| `NotesMarkdown` | `nvarchar?` | Existing |

**New nav property** (not a column):  
`ICollection<ReleaseRepository> ReleaseRepositories`

---

### New: `ReleaseRepository`

Join entity linking a `Release` to a `Repository`. Captures a historical snapshot of the repo's participation in the release. Created at release creation; snapshot fields are immutable once `Release.Status = Published`.

| Column | Type | Notes |
|--------|------|-------|
| `Id` | `int` PK | |
| `ReleaseId` | `int` FK → `Releases` | Cascade delete |
| `RepositoryId` | `int` FK → `Repositories` | Restrict delete |
| `PreviousVersion` | `nvarchar` | Semver string at creation time; empty for backfilled legacy rows |
| `NextVersion` | `nvarchar` | User-confirmed next version |
| `BumpType` | `nvarchar` | `"major"`, `"minor"`, `"patch"`, or `"manual"` |
| `FromCommitSha` | `nvarchar` | Commit at `PreviousVersion` tag; empty for legacy |
| `ToCommitSha` | `nvarchar` | HEAD on default branch at creation; empty for legacy |
| `CommitCount` | `int` | Number of commits in the range; 0 for legacy |
| `TicketCount` | `int` | Unique Jira ticket IDs referenced in the range; 0 for legacy |

**Indexes**:
- Unique on `(ReleaseId, RepositoryId)` — a repo appears at most once per release.
- Non-unique on `RepositoryId` — enables "all releases for this repo" queries.

**Delete behaviour**:
- `Release` deleted → `ReleaseRepository` rows deleted (`Cascade`).
- `Repository` deleted → blocked if `ReleaseRepository` rows exist (`Restrict`). Use the existing untrack/archive flow instead.

---

## State Machine: `Release.Status`

```
[Created] → Draft
   ↓  (Admin publishes)
Published
   ↓  (Admin archives)
Archived
```

**Rules enforced by `IReleaseCompositionService`**:
- `CreateDraftAsync` → sets `Status = Draft`.
- `UpdateDraftAsync` → allowed only when `Status = Draft`; throws `ConflictException(release_not_draft)` otherwise.
- Publish / Archive → handled by existing `IReleaseService`; not changed by this feature.

**Snapshot immutability rule** (enforced at service layer):
- While `Draft`: `ReleaseRepository` fields may be replaced wholesale on each `UpdateDraftAsync` call.
- Once `Published`: `ReleaseRepository` fields are frozen. The service MUST NOT re-derive or overwrite them.

---

## EF Core Migration: `AddReleaseRepository`

### Up()

1. `CreateTable("ReleaseRepositories", ...)` with all columns above.
2. `CreateIndex("IX_ReleaseRepositories_ReleaseId_RepositoryId", unique: true)`.
3. `CreateIndex("IX_ReleaseRepositories_RepositoryId")`.
4. Add FK from `ReleaseRepositories.ReleaseId` → `Releases.Id` (Cascade).
5. Add FK from `ReleaseRepositories.RepositoryId` → `Repositories.Id` (Restrict).
6. **Backfill SQL** (raw SQL after `CreateTable`):

```sql
INSERT INTO ReleaseRepositories
    (ReleaseId, RepositoryId, PreviousVersion, NextVersion, BumpType,
     FromCommitSha, ToCommitSha, CommitCount, TicketCount)
SELECT
    r.Id,
    p.PrimaryRepositoryId,
    '',
    r.Version,
    'manual',
    '', '', 0, 0
FROM Releases r
JOIN Projects p ON p.Id = r.ProjectId
WHERE p.PrimaryRepositoryId IS NOT NULL;
```

> Legacy rows intentionally have empty snapshot fields. The UI renders them with em-dash placeholders and a "Pre-feature release — partial data" badge.

### Down()

1. Drop FK constraints.
2. `DropTable("ReleaseRepositories")`.

---

## Validation Rules

| Field | Rule | Error |
|-------|------|-------|
| `Repositories` (list) | Must contain at least one entry | `at_least_one_repo_required` |
| Each `RepositoryId` | Must belong to the project via `ProjectRepository` | `repo_not_in_project` |
| Each `NextVersion` | Must be a valid semver string | `invalid_semver` |
| Each `NextVersion` | Must be strictly greater than the freshly-fetched `PreviousVersion` | `version_not_greater` |
| Each `BumpType` | Must be one of `major`, `minor`, `patch`, `manual` | `invalid_bump_type` |
| Update when not Draft | `Release.Status != Draft` | `release_not_draft` (409 Conflict) |

**Server-discarded client fields** (always re-derived server-side):  
`PreviousVersion`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, `TicketCount`.
