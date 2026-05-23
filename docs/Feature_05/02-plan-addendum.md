# Plan Addendum — Per-Repo Release Versioning

> Append the following technical decisions to the existing plan. Reuse existing services, DTOs, and patterns where noted.

## Data Model Changes

### Modify: `Release`

Add a navigation property to the new join entity. Keep `Version` as-is (its semantics shift from "user-entered" to "derived" but the column is unchanged — purely a write-path change).

```csharp
public class Release
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Version { get; set; } = "";          // DERIVED from primary repo's NextVersion at save time
    public ReleaseStatus Status { get; set; }          // Draft | Published | Archived
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ConfluencePageUrl { get; set; }
    public string? NotesMarkdown { get; set; }

    // NEW
    public ICollection<ReleaseRepository> ReleaseRepositories { get; set; } = new List<ReleaseRepository>();
}
```

### New: `ReleaseRepository`

Join entity that captures each repo's participation in a release plus the historical snapshot.

```csharp
public class ReleaseRepository
{
    public int Id { get; set; }

    public int ReleaseId { get; set; }
    public Release Release { get; set; } = null!;

    public int RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;

    // Versioning
    public string PreviousVersion { get; set; } = "";   // e.g., "1.30.0" at creation time
    public string NextVersion { get; set; } = "";       // e.g., "1.31.0" — user-confirmed
    public string BumpType { get; set; } = "";          // "major" | "minor" | "patch" | "manual"

    // Snapshot of the change range
    public string FromCommitSha { get; set; } = "";     // commit at PreviousVersion tag
    public string ToCommitSha { get; set; } = "";       // HEAD on default branch at creation
    public int CommitCount { get; set; }
    public int TicketCount { get; set; }
}
```

**EF Core configuration:**

- Unique index on `(ReleaseId, RepositoryId)` — a repo appears at most once per release.
- Index on `RepositoryId` alone — enables later "which releases included this repo" queries cheaply.
- `OnDelete(DeleteBehavior.Cascade)` from `Release` → `ReleaseRepository`.
- `OnDelete(DeleteBehavior.Restrict)` from `Repository` → `ReleaseRepository`. A repo that participated in a release cannot be hard-deleted; existing untrack/archive flow remains the safe path.

**Why snapshot `PreviousVersion`, `FromCommitSha`, `ToCommitSha`, and counts on the join row?**

A release is a historical record. Tags can be deleted, branches force-pushed, repos detached from the project. Without snapshotting, a published release shown six months later would show wrong "previous version" data the moment something upstream changes. The snapshot makes published releases stable artifacts — consistent with how the existing reconciliation snapshot is designed.

### Migration

Add a new EF Core migration `AddReleaseRepository` that:

1. Creates the `ReleaseRepositories` table.
2. Backfills one `ReleaseRepository` row per existing `Release`, referencing the project's primary repo with:
   - `NextVersion = Release.Version`
   - `BumpType = "manual"`
   - `PreviousVersion = ""`, `FromCommitSha = ""`, `ToCommitSha = ""`, `CommitCount = 0`, `TicketCount = 0`

The backfill SQL belongs in the migration's `Up()` as raw SQL after the `CreateTable` call. Legacy rows are intentionally informational-only — re-rendering the per-repo table for a pre-feature release will show empty snapshot fields, and that's acceptable.

## Service Interfaces

### Reuse: `IVersionBumpService`

The existing version-bump logic (used today by the single-version wizard) already knows how to suggest `major`/`minor`/`patch` from a commit range. Move it behind an interface if it isn't already, and call it **per repo** instead of per project.

```csharp
public interface IVersionBumpService
{
    Task<VersionBumpSuggestionDto> SuggestAsync(
        int repositoryId,
        CancellationToken ct = default);
}

public record VersionBumpSuggestionDto(
    string PreviousVersion,
    string SuggestedNextVersion,
    string BumpType,             // "major" | "minor" | "patch"
    string FromCommitSha,
    string ToCommitSha,
    int CommitCount,
    int TicketCount);
```

### New: `IReleaseCompositionService`

Encapsulates the multi-repo composition logic. Sits next to the existing `IReleaseService`; it does not replace it. `IReleaseService` continues to own publish, archive, Confluence push.

```csharp
public interface IReleaseCompositionService
{
    /// <summary>
    /// Returns per-repo suggestions for the wizard. Read-only. Does not persist.
    /// </summary>
    Task<ReleasePreviewDto> PreviewAsync(
        int projectId,
        IReadOnlyList<int> repositoryIds,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a Draft release with the given per-repo selections.
    /// Re-captures snapshot fields server-side; client-supplied PreviousVersion is ignored.
    /// </summary>
    Task<ReleaseDto> CreateDraftAsync(
        int projectId,
        CreateReleaseRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates a Draft release's repo selection and/or per-repo versions.
    /// Throws ConflictException if status is not Draft.
    /// </summary>
    Task<ReleaseDto> UpdateDraftAsync(
        int releaseId,
        UpdateReleaseRequest request,
        CancellationToken ct = default);
}

public record ReleasePreviewDto(
    IReadOnlyList<ReleasePreviewRepoDto> Repositories,
    string DerivedReleaseVersion,
    int DerivedFromRepositoryId);

public record ReleasePreviewRepoDto(
    int RepositoryId,
    string Name,
    bool IsPrimary,
    bool HasChanges,
    string PreviousVersion,
    string SuggestedNextVersion,
    string BumpType,
    int CommitCount,
    int TicketCount);

public record CreateReleaseRequest(
    string Name,
    IReadOnlyList<ReleaseRepositorySelectionDto> Repositories);

public record ReleaseRepositorySelectionDto(
    int RepositoryId,
    string NextVersion,
    string BumpType);             // "major" | "minor" | "patch" | "manual"
```

**Derivation rule** (implemented inside `PreviewAsync` and `CreateDraftAsync`):

```csharp
private (string version, int repoId) DeriveReleaseVersion(
    IEnumerable<ReleaseRepositorySelectionDto> selections,
    Project project)
{
    var primarySelection = selections.FirstOrDefault(s =>
        s.RepositoryId == project.PrimaryRepositoryId);

    if (primarySelection is not null)
        return (primarySelection.NextVersion, primarySelection.RepositoryId);

    var fallback = selections
        .Select(s => new { Sel = s, Repo = project.ProjectRepositories.First(pr => pr.RepositoryId == s.RepositoryId).Repository })
        .OrderBy(x => x.Repo.Name, StringComparer.OrdinalIgnoreCase)
        .First();

    return (fallback.Sel.NextVersion, fallback.Sel.RepositoryId);
}
```

## API Endpoints

All routes are project-scoped under `/api/projects/{projectId}/releases`. Authentication required; Admin role required for write operations.

| Method | Route | Purpose | Role |
|--------|-------|---------|------|
| `GET`  | `/api/projects/{projectId}/releases` | List releases for the project | Viewer |
| `POST` | `/api/projects/{projectId}/releases/preview` | Compute per-repo suggestions for the wizard | Admin |
| `POST` | `/api/projects/{projectId}/releases` | Create a Draft release | Admin |
| `GET`  | `/api/projects/{projectId}/releases/{id}` | Get a single release with `ReleaseRepositories` | Viewer |
| `PUT`  | `/api/projects/{projectId}/releases/{id}` | Update a Draft release | Admin |
| `DELETE` | `/api/projects/{projectId}/releases/{id}` | Delete (Draft only) | Admin |

### `GET /api/projects/{projectId}/releases`

Returns the shape the Releases list view needs. Default `?sort=createdAt&order=desc`. Supports `?status=Draft|Published|Archived` and `?search=<name>`.

```json
[
  {
    "id": 42,
    "name": "May Release",
    "version": "2.6.0",
    "status": "Published",
    "createdAt": "2026-05-20T14:02:00Z",
    "publishedAt": "2026-05-20T15:10:00Z",
    "repoCount": 3
  }
]
```

### `POST /api/projects/{projectId}/releases/preview`

Request:

```json
{ "repositoryIds": [11, 12, 15] }
```

Response: `ReleasePreviewDto` shape from above. Validates that all `repositoryIds` belong to the project; returns `400 ValidationProblem` otherwise.

### `POST /api/projects/{projectId}/releases`

Request: `CreateReleaseRequest` shape from above. Server-side validation:

1. Every `RepositoryId` belongs to the project (`ProjectRepository` exists).
2. At least one repository is provided.
3. Each `NextVersion` is a valid semver string.
4. Each `NextVersion` is strictly greater than the freshly-fetched `PreviousVersion` for that repo.
5. Each `BumpType` is one of the allowed values.

Server discards any client-sent `PreviousVersion`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, or `TicketCount` — these are always captured server-side using `IVersionBumpService.SuggestAsync` at the moment of creation.

Returns `201 Created` with the full `ReleaseDto` including the `ReleaseRepositories` collection.

### `PUT /api/projects/{projectId}/releases/{id}`

- If status is not `Draft`, returns `409 Conflict` with code `release_not_draft`.
- Otherwise replaces the `ReleaseRepository` collection wholesale and re-derives `Version`. Snapshot fields are re-captured.

## Frontend Components

### `ReleaseWizard` — new step component

Add a new step between **"Confirm change range"** and **"Choose template"**.

- Component: `<ReleaseRepoSelectionStep />` under `frontend/src/features/releases/wizard/`.
- Loads data via `POST /releases/preview` on mount, then re-calls whenever the user toggles a checkbox or changes a bump type to recompute counts for the new selection set.
- Form state managed with React Hook Form. Zod schema validates per-row semver-greater-than constraint client-side mirroring the server rule.
- Submit handler calls `POST /releases` and routes to the existing release detail page on success.

### `ProjectReleasesList` — new view

- Component: `<ProjectReleasesList projectId={projectId} />` rendered as a new tab on the existing project detail page.
- Data via TanStack Query: `useQuery(['project', projectId, 'releases', filters], ...)`.
- shadcn `<Table />` with sortable headers; row click navigates to `/projects/{projectId}/releases/{releaseId}`.
- Status filter as a shadcn `<Select />`; name search as a debounced `<Input />`.

### `ReleaseDetailPage` — update

The existing release detail page must render the per-repo table. Add a `<ReleaseRepositoriesTable />` section showing: repo name, previous → next, bump type badge, commit count, ticket count.

For legacy releases (backfilled with empty snapshot fields), the table renders the single primary-repo row with em-dashes in the empty columns and a small "Pre-feature release — partial data" badge.

## Test Strategy

- **Unit**: `IReleaseCompositionService.DeriveReleaseVersion` covered for: primary-included, primary-excluded fallback, single-repo release. `IVersionBumpService.SuggestAsync` covered for: no commits, only fixes, mixed feat/fix, breaking-change footer, breaking `!` marker.
- **Integration** (Testcontainers + SQLite): full create-release flow end-to-end — preview → create → fetch — asserting snapshot fields are populated server-side and `Release.Version` matches the primary repo's `NextVersion`.
- **Integration**: backfill migration on a database containing legacy `Release` rows produces exactly one `ReleaseRepository` per release.
- **E2E** (Playwright if it exists in the repo, otherwise skip): create-release wizard happy path with three repos selected.

## Out-of-Plan Notes for `/analyze`

- The Jira reconciliation feature anchors on `(RepositoryId, NextVersion)`. After this feature lands, reconciliation's per-release view should iterate over `Release.ReleaseRepositories` instead of `Project.ProjectRepositories`. **This change is explicitly a follow-up** and should be flagged by `/analyze` as an inconsistency to address in a separate feature branch — do not bundle it here.
