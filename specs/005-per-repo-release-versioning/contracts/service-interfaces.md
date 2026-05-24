# Service Interface Contracts: Per-Repo Release Versioning

**Phase**: 1 — Contracts  
**Branch**: `006-per-repo-release-versioning`  
**Date**: 2026-05-23

All interfaces live in `RepoManager.Application`. Implementations live in `RepoManager.Infrastructure`. Every async method accepts `CancellationToken ct = default` as the last parameter.

---

## IVersionBumpService

Suggests a next version for a single repository based on its commit range since the latest semver tag. **Read-only — does not persist anything.**

```csharp
public interface IVersionBumpService
{
    Task<VersionBumpSuggestionDto> SuggestAsync(
        int repositoryId,
        CancellationToken ct = default);
}

public record VersionBumpSuggestionDto(
    string PreviousVersion,    // latest semver tag on the repo; "" if no tag
    string SuggestedNextVersion,
    string BumpType,           // "major" | "minor" | "patch"
    string FromCommitSha,      // commit at PreviousVersion tag; "" if no tag
    string ToCommitSha,        // HEAD on default branch
    int CommitCount,
    int TicketCount);
```

**Bump type derivation rules** (in priority order):
1. Any commit in range has `BREAKING CHANGE` footer or `!` after the type → `major`
2. Any commit has type `feat` → `minor`
3. Otherwise → `patch`

**Edge cases**:
- No tag: `PreviousVersion = ""`, `FromCommitSha = ""`, `SuggestedNextVersion = "0.1.0"`, `BumpType = "minor"`.
- No commits since last tag: `CommitCount = 0`, `TicketCount = 0`; bump type is `patch` (no-op version bump still representable).
- Non-semver tag: throw `ValidationException` with code `non_semver_tag`.

---

## IReleaseCompositionService

Encapsulates multi-repo release composition: preview, create, and update-draft. **Does not own publish, archive, or Confluence push** — those remain on `IReleaseService`.

```csharp
public interface IReleaseCompositionService
{
    Task<ReleasePreviewDto> PreviewAsync(
        int projectId,
        IReadOnlyList<int> repositoryIds,
        CancellationToken ct = default);

    Task<ReleaseDto> CreateDraftAsync(
        int projectId,
        CreateReleaseRequest request,
        CancellationToken ct = default);

    Task<ReleaseDto> UpdateDraftAsync(
        int releaseId,
        UpdateReleaseRequest request,
        CancellationToken ct = default);
}
```

### DTOs

```csharp
// PreviewAsync response
public record ReleasePreviewDto(
    IReadOnlyList<ReleasePreviewRepoDto> Repositories,
    string DerivedReleaseVersion,
    int DerivedFromRepositoryId);

public record ReleasePreviewRepoDto(
    int RepositoryId,
    string Name,
    bool IsPrimary,
    bool HasChanges,           // CommitCount > 0
    string PreviousVersion,
    string SuggestedNextVersion,
    string BumpType,
    int CommitCount,
    int TicketCount);

// CreateDraftAsync / UpdateDraftAsync request
public record CreateReleaseRequest(
    string Name,
    IReadOnlyList<ReleaseRepositorySelectionDto> Repositories);

public record UpdateReleaseRequest(
    IReadOnlyList<ReleaseRepositorySelectionDto> Repositories);

public record ReleaseRepositorySelectionDto(
    int RepositoryId,
    string NextVersion,
    string BumpType);   // "major" | "minor" | "patch" | "manual"

// Shared release response DTO (also used by existing IReleaseService)
public record ReleaseDto(
    int Id,
    int ProjectId,
    string Name,
    string Version,
    string Status,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    string? ConfluencePageUrl,
    string? NotesMarkdown,
    IReadOnlyList<ReleaseRepositoryDto> ReleaseRepositories);

public record ReleaseRepositoryDto(
    int Id,
    int RepositoryId,
    string RepositoryName,
    string PreviousVersion,
    string NextVersion,
    string BumpType,
    string FromCommitSha,
    string ToCommitSha,
    int CommitCount,
    int TicketCount,
    bool IsLegacy);   // true when all snapshot fields are empty (backfilled)
```

### Version derivation rule

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
        .Select(s => new {
            Sel = s,
            Repo = project.ProjectRepositories
                .First(pr => pr.RepositoryId == s.RepositoryId).Repository
        })
        .OrderBy(x => x.Repo.Name, StringComparer.OrdinalIgnoreCase)
        .First();

    return (fallback.Sel.NextVersion, fallback.Sel.RepositoryId);
}
```

### Exception contracts

| Condition | Exception Type | Code |
|-----------|---------------|------|
| Project not found | `NotFoundException` | — |
| Release not found | `NotFoundException` | — |
| Release not Draft (on update) | `ConflictException` | `release_not_draft` |
| RepositoryId not in project | `ValidationException` | `repo_not_in_project` |
| Empty repository list | `ValidationException` | `at_least_one_repo_required` |
| Invalid semver `NextVersion` | `ValidationException` | `invalid_semver` |
| `NextVersion` not greater than previous | `ValidationException` | `version_not_greater` |
| Invalid `BumpType` | `ValidationException` | `invalid_bump_type` |
| Non-semver repo tag | `ValidationException` | `non_semver_tag` |

---

## IReleaseService (existing — unchanged)

The existing service keeps its current interface. It is not modified by this feature.

`CreateDraftAsync` / `UpdateDraftAsync` are **removed** from `IReleaseService` (if they exist there) and moved to `IReleaseCompositionService`. `PublishAsync`, `ArchiveAsync`, `DeleteAsync`, `GenerateNotesAsync`, and `PublishToConfluenceAsync` remain on `IReleaseService`.

> Confirm with `IReleaseService` implementation during development. If it does not currently have create/update methods, no removal is needed.

---

## Validator: CreateReleaseRequestValidator

Located in `RepoManager.Application`, registered with FluentValidation DI.

```
RuleFor(x => x.Name).NotEmpty().MaximumLength(200)
RuleFor(x => x.Repositories).NotEmpty()
    .WithMessage("At least one repository must be selected.")
    .WithErrorCode("at_least_one_repo_required")
ForEach(x => x.Repositories):
  RuleFor(r => r.NextVersion).Must(BeValidSemver)
    .WithErrorCode("invalid_semver")
  RuleFor(r => r.BumpType).Must(BeValidBumpType)
    .WithErrorCode("invalid_bump_type")
```

Cross-field rules (repo membership, version ordering) are enforced in the service after loading project context — not in the validator, because they require database access.
