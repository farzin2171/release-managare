# Service Interfaces: Project Page Templates

**Branch**: `007-project-page-templates`
**Date**: 2026-05-24

---

## New Services

### IProjectTemplateBindingService

Located in `RepoManager.Application/Services/`.

```csharp
public interface IProjectTemplateBindingService
{
    Task<IReadOnlyList<ProjectTemplateBindingDto>> GetAllAsync(
        int projectId, CancellationToken ct = default);

    Task<ProjectTemplateBindingDto> CreateAsync(
        int projectId, CreateBindingRequest request, CancellationToken ct = default);

    Task<ProjectTemplateBindingDto> UpdateAsync(
        int projectId, int bindingId, UpdateBindingRequest request, CancellationToken ct = default);

    Task DeleteAsync(
        int projectId, int bindingId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTemplateBindingDto>> ReorderAsync(
        int projectId, IReadOnlyList<int> orderedIds, CancellationToken ct = default);
}
```

**Error contracts**:
- `NotFoundException` — project or binding not found.
- `ConflictException("last_release_notes_binding")` — attempt to delete the only `ReleaseNotes` binding.
- `ConflictException("duplicate_release_notes_binding")` — attempt to create a second `ReleaseNotes` binding.
- `ValidationException` — invalid request fields.

---

### IProjectCustomVariableService

Located in `RepoManager.Application/Services/`.

```csharp
public interface IProjectCustomVariableService
{
    Task<IReadOnlyList<ProjectCustomVariableDto>> GetAllAsync(
        int projectId, CancellationToken ct = default);

    Task<ProjectCustomVariableDto> UpsertAsync(
        int projectId, string key, string value, CancellationToken ct = default);

    Task DeleteAsync(
        int projectId, string key, CancellationToken ct = default);
}
```

**Error contracts**:
- `NotFoundException` — project not found; key not found (on delete).
- `ValidationException` — key pattern violation or value length exceeded.

---

### IReleaseRenderService

Located in `RepoManager.Application/Services/`. Core of this feature.

```csharp
public interface IReleaseRenderService
{
    /// <summary>
    /// Builds the render context and renders all bound templates for the release.
    /// Returns the PreparedRelease ready for wizard preview.
    /// </summary>
    Task<PreparedReleaseDto> PrepareAsync(
        int releaseId,
        PreparePageRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Publishes the client-submitted (possibly edited) prepared pages to Confluence
    /// in SortOrder, then cross-links from the primary ReleaseNotes page.
    /// </summary>
    Task<PublishResultDto> PublishAsync(
        int releaseId,
        PublishPagesRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Renders a single template against a synthetic or project-latest-release context.
    /// Used by the Templates settings preview.
    /// </summary>
    Task<TemplatePreviewDto> PreviewTemplateAsync(
        int templateId,
        TemplatePreviewRequest request,
        CancellationToken ct = default);
}
```

**Error contracts**:
- `NotFoundException` — release, project, or template not found.
- `ValidationException("no_release_notes_binding")` — project has no `ReleaseNotes` binding.
- `ValidationException("no_semver_tag")` — version-primary repo has no semver tag (unless `adminOverrideVersion` is supplied).
- `ExternalServiceException` — Confluence API failure.

---

## DTOs (Application layer — record types)

```csharp
// Binding
public record ProjectTemplateBindingDto(
    int Id, int ProjectId, int TemplateId, string TemplateName,
    string Kind, string PageTitleTemplate, string? ParentPageId,
    bool LinkFromReleaseNotes, int SortOrder);

public record CreateBindingRequest(
    int TemplateId, string Kind, string PageTitleTemplate,
    string? ParentPageId, bool LinkFromReleaseNotes, int SortOrder);

public record UpdateBindingRequest(
    int? TemplateId, string? Kind, string? PageTitleTemplate,
    string? ParentPageId, bool? LinkFromReleaseNotes, int? SortOrder);

// Custom variables
public record ProjectCustomVariableDto(string Key, string Value);

// Render context
public record ReleaseRenderContextDto(
    ProjectInfoDto Project, string Version, string PreviousVersion,
    DateTimeOffset ReleaseDate,
    IReadOnlyList<RepoContextDto> Repositories,
    TicketBucketsDto Tickets,
    IReadOnlyList<ContributorDto> Contributors,
    ReconciliationSummaryDto? Reconciliation,
    ConfluenceTargetDto Confluence,
    IReadOnlyDictionary<string, string> Custom);

public record RepoContextDto(
    string Name, string PreviousTag, string NextTag,
    int CommitCount, int TicketCount, string JiraFixVersion);

public record TicketBucketsDto(
    IReadOnlyList<TicketDto> Breaking,
    IReadOnlyList<TicketDto> Features,
    IReadOnlyList<TicketDto> Fixes,
    IReadOnlyList<TicketDto> Other);

public record ReconciliationSummaryDto(
    int MatchedCount, int JiraOnlyCount, int GitOnlyCount,
    double MatchRate, DateTimeOffset RunAt);

// Prepared pages
public record PreparedPageDto(
    int BindingId, string Kind, string Title, string Body,
    string ParentPageId, bool LinkFromReleaseNotes, int SortOrder,
    IReadOnlyList<string> UnknownTokens);

public record PreparedReleaseDto(
    ReleaseRenderContextDto Context,
    IReadOnlyList<PreparedPageDto> Pages,
    IReadOnlyList<string> Warnings);

// Prepare request
public record PreparePageRequest(
    string? AdminOverrideVersion,
    ReconciliationSummaryDto? ReconciliationData);

// Publish
public record PublishPagesRequest(IReadOnlyList<PublishPageDto> Pages);

public record PublishPageDto(
    int BindingId, string Title, string Body,
    string ParentPageId, bool LinkFromReleaseNotes, int SortOrder);

public record PublishResultDto(IReadOnlyList<PublishedPageDto> PublishedPages);

public record PublishedPageDto(
    int BindingId, string ConfluencePageId,
    string ConfluenceUrl, string Title);

// Template preview
public record TemplatePreviewRequest(string ContextSource, int? ProjectId);

public record TemplatePreviewDto(
    string RenderedTitle, string RenderedBody,
    IReadOnlyList<string> UnknownTokens,
    string ContextSource, string? ProjectName, string? ReleaseVersion);
```

---

## Validators (FluentValidation)

```csharp
// CreateBindingRequestValidator
// - TemplateId > 0
// - Kind: must be in ["ReleaseNotes", "Checklist", "Custom"]
// - PageTitleTemplate: NotEmpty, MaxLength(500)
// - ParentPageId: MaxLength(100) when not null
// - SortOrder: >= 0

// PublishPagesRequestValidator
// - Pages: NotEmpty
// - Each page.Title: NotEmpty, MaxLength(255)
// - Each page.BindingId: > 0

// ProjectCustomVariableUpsertValidator
// - Key: NotEmpty, MaxLength(50), Matches("[a-zA-Z][a-zA-Z0-9_]*")
// - Value: MaxLength(500)
```

---

## Handlebars Helpers (registered at app startup)

| Helper | Signature | Example |
|--------|-----------|---------|
| `formatDate` | `(date, format)` → string | `{{formatDate releaseDate "yyyy-MM-dd"}}` |
| `length` | `(collection)` → int | `{{length tickets.features}}` |
| `eq` | `(a, b)` → bool | `{{#if (eq version "1.0.0")}}` |
| `gt` | `(a, b)` → bool | `{{#if (gt (length tickets.breaking) 0)}}` |
| `minus` | `(a, b)` → number | `{{minus totalCount matchedCount}}` |
| `lower` | `(s)` → string | `{{lower project.name}}` |
| `upper` | `(s)` → string | `{{upper project.name}}` |
| `truncate` | `(s, maxLen)` → string | `{{truncate ticket.summary 80}}` |
| `jiraLink` | `(ticketId)` → string | `{{jiraLink "PAY-123"}}` → Jira URL |

All helpers registered via `HandlebarsDotNet.Handlebars.RegisterHelper(...)` during `IServiceCollection.AddHandlebars()` startup extension.

---

## MissingTokenRecorder (Infrastructure internal)

```csharp
// Located in RepoManager.Infrastructure/Services/Handlebars/MissingTokenRecorder.cs
// Implements IFormatterProvider
// [ThreadStatic] HashSet<string> _bag captures unknown token paths
// PageRenderer calls BeginCapture() before render, EndCapture() after
// Returns IReadOnlySet<string> of unknown token names
```

---

## Frontend Store

```typescript
// src/features/releases/wizard/store/useWizardStore.ts
// Zustand store persisted to sessionStorage

interface WizardState {
  projectId: number | null
  releaseId: number | null
  pages: PreparedPageSlot[]
  reconciliation: {
    ran: boolean
    stale: boolean
    data: ReconciliationSummaryDto | null
  }
  // actions
  initPages(pages: PreparedPageDto[]): void
  editPage(bindingId: number, title: string, body: string): void
  reRenderPages(freshPages: PreparedPageDto[]): void
  resolveConflict(bindingId: number, choice: 'keep' | 'discard'): void
  setReconciliationData(data: ReconciliationSummaryDto): void
  markReconciliationStale(): void
  resetWizard(): void
}

type DraftState =
  | { kind: 'server' }
  | { kind: 'edited'; title: string; body: string }
  | { kind: 'conflict'; serverTitle: string; serverBody: string;
      draftTitle: string; draftBody: string }

interface PreparedPageSlot {
  bindingId: number
  serverTitle: string
  serverBody: string
  sortOrder: number
  draftState: DraftState
}
```
