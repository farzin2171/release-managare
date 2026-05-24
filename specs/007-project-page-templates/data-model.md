# Data Model: Project Page Templates

**Branch**: `007-project-page-templates`
**Date**: 2026-05-24

## New Entities

### ProjectTemplateBinding

A directed association between a logical project and a release-note template, specifying how the template is rendered and published for a release.

```
ProjectTemplateBinding
├── Id                   int          PK, auto-increment
├── ProjectId            int          FK → Project.Id, NOT NULL, CASCADE DELETE
├── TemplateId           int          FK → ReleaseNoteTemplate.Id, NOT NULL, RESTRICT DELETE
├── Kind                 string       NOT NULL  ("ReleaseNotes" | "Checklist" | "Custom")
├── PageTitleTemplate    string       NOT NULL, max 500 chars  (Handlebars expression)
├── ParentPageId         string?      NULL = use project default Confluence parent
├── LinkFromReleaseNotes bool         NOT NULL, default false
├── SortOrder            int          NOT NULL, default 0
├── CreatedAt            DateTimeOffset  NOT NULL
└── UpdatedAt            DateTimeOffset  NOT NULL
```

**Constraints**:
- Unique index on `(ProjectId, SortOrder)` — enforced loosely; reorder operations swap values atomically in a transaction.
- Exactly one row with `Kind = 'ReleaseNotes'` per project — enforced in service layer (`ProjectTemplateBindingService.DeleteAsync` throws `ConflictException` if deleting the last `ReleaseNotes` binding).
- `TemplateId` RESTRICT on delete: deleting a `ReleaseNoteTemplate` that is referenced by any binding is blocked by the FK; admin must unbind first.

**EF Core config notes**:
- `HasIndex(b => new { b.ProjectId, b.SortOrder })` — non-unique (SortOrder uniqueness is transactional, not a DB constraint, because reorder uses temp values).
- `HasCheckConstraint("CK_Binding_Kind", "Kind IN ('ReleaseNotes','Checklist','Custom')")`.

---

### ProjectCustomVariable

A scoped string key/value pair attached to a project, exposed in templates as `{{custom.<key>}}`.

```
ProjectCustomVariable
├── Id           int          PK, auto-increment
├── ProjectId    int          FK → Project.Id, NOT NULL, CASCADE DELETE
├── Key          string       NOT NULL, max 50 chars, pattern: [a-zA-Z][a-zA-Z0-9_]*
├── Value        string       NOT NULL, max 500 chars (plain text, never encrypted)
├── CreatedAt    DateTimeOffset  NOT NULL
└── UpdatedAt    DateTimeOffset  NOT NULL
```

**Constraints**:
- Unique index on `(ProjectId, Key)` — `UpsertAsync` uses this to decide insert vs update.
- `HasCheckConstraint("CK_CustomVar_Key", "Key GLOB '[a-zA-Z]*'")` — validates key starts with a letter (SQLite GLOB pattern).

---

## Modified Entities

### Project (additions)

```diff
Project
+ VersionBumpStrategy    string    NOT NULL, default 'Minor'  ("Patch" | "Minor" | "Major")
- DefaultReleaseNoteTemplateId  int?   ← DROPPED by migration
```

**EF Core config**:
- `HasCheckConstraint("CK_Project_VersionBumpStrategy", "VersionBumpStrategy IN ('Patch','Minor','Major')")`.
- `HasDefaultValue("Minor")` on `VersionBumpStrategy`.

---

## Relationships

```
Project (1) ──< ProjectTemplateBinding (N)
Project (1) ──< ProjectCustomVariable (N)
ReleaseNoteTemplate (1) ──< ProjectTemplateBinding (N)
```

No changes to `Release`, `ReleaseRepository`, `Commit`, or `Ticket` tables.

---

## Migration: AddProjectTemplateBindings

**Migration name**: `AddProjectTemplateBindings`

**Up() steps** (all in one transaction):

1. Add `Project.VersionBumpStrategy` column (NOT NULL, default `'Minor'`).

2. Create `ProjectTemplateBindings` table with all columns and constraints.

3. Create `ProjectCustomVariables` table with all columns and constraints.

4. **Data backfill** — insert one `ProjectTemplateBinding` of kind `ReleaseNotes` for every project with a non-null `DefaultReleaseNoteTemplateId`:
   ```sql
   INSERT INTO ProjectTemplateBindings
       (ProjectId, TemplateId, Kind, PageTitleTemplate, ParentPageId,
        LinkFromReleaseNotes, SortOrder, CreatedAt, UpdatedAt)
   SELECT
       Id,
       DefaultReleaseNoteTemplateId,
       'ReleaseNotes',
       '{{project.name}} {{version}} — Release Notes',
       NULL,
       0,
       0,
       datetime('now'),
       datetime('now')
   FROM Projects
   WHERE DefaultReleaseNoteTemplateId IS NOT NULL;
   ```

5. Drop `Project.DefaultReleaseNoteTemplateId` column.

**Down() steps**:

1. Re-add `Project.DefaultReleaseNoteTemplateId` column (nullable).
2. Restore values from `ProjectTemplateBindings` (kind = ReleaseNotes, sort order 0).
3. Drop `ProjectCustomVariables` table.
4. Drop `ProjectTemplateBindings` table.
5. Drop `Project.VersionBumpStrategy` column.

---

## Ephemeral Structures (not persisted)

These are computed at runtime during wizard sessions; no database tables are created for them.

### ReleaseRenderContext (Application DTO)

Feeds every Handlebars render. Constructed by `ReleaseRenderService.BuildContextAsync`.

```
ReleaseRenderContext
├── project
│   ├── id            int
│   ├── name          string
│   └── description   string
├── version           string           (semver, e.g., "1.31.0")
├── previousVersion   string           (semver, e.g., "1.30.0")
├── releaseDate       DateTimeOffset
├── repositories[]
│   ├── name          string
│   ├── previousTag   string
│   ├── nextTag       string
│   ├── commitCount   int
│   ├── ticketCount   int
│   └── jiraFixVersion  string         ("<RepoName>_<NextVersion>")
├── tickets
│   ├── breaking[]    TicketDto[]
│   ├── features[]    TicketDto[]
│   ├── fixes[]       TicketDto[]
│   └── other[]       TicketDto[]
├── contributors[]    ContributorDto[]
├── reconciliation    ReconciliationSummaryDto?   (null if not run)
├── confluence
│   ├── spaceKey      string
│   └── parentPageId  string
└── custom            Dictionary<string, string>  (ProjectCustomVariable values)
```

### PreparedPage (Application DTO)

Represents one rendered page before/during publish.

```
PreparedPage
├── bindingId         int
├── kind              string
├── title             string          (rendered, validated ≤ 255 chars, non-empty)
├── body              string          (rendered Handlebars output)
├── parentPageId      string          (resolved: override ?? project default)
├── linkFromReleaseNotes  bool
├── sortOrder         int
└── unknownTokens     string[]        (non-blocking warning set)
```

### PreparedRelease (API response DTO)

```
PreparedRelease
├── context           ReleaseRenderContext
├── pages             PreparedPage[]   (ordered by SortOrder)
└── warnings          string[]         (collected across all pages: title collisions, etc.)
```

---

## Validation Rules

| Field | Rule |
|-------|------|
| `ProjectTemplateBinding.PageTitleTemplate` | Required; 1–500 chars; must render to 1–255 chars (checked at wizard prepare time, not at save time) |
| `ProjectTemplateBinding.Kind` | One of: `ReleaseNotes`, `Checklist`, `Custom` |
| `ProjectCustomVariable.Key` | 1–50 chars; pattern `[a-zA-Z][a-zA-Z0-9_]*` |
| `ProjectCustomVariable.Value` | 0–500 chars |
| `Project.VersionBumpStrategy` | One of: `Patch`, `Minor`, `Major` |
| `PreparedPage.title` (at publish time) | Non-empty; max 255 chars; enforced by `PublishPagesRequestValidator` |
| Duplicate prepared page titles | Detected in `ReleaseRenderService.ValidatePreparedPages`; surface as warning, not error |
