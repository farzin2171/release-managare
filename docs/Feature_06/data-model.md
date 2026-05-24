# Data Model: Project Page Templates

**Phase**: 1
**Status**: Final

This document specifies the database schema changes, entity definitions, and migration plan for the feature. It is the source of truth for the EF Core migrations listed in `tasks.md` (T011–T014).

---

## Entities

### `ProjectTemplateBinding` (new)

A many-to-one association from a logical project to a release-note template, plus metadata describing how that template is used at release time.

| Property | Type | Notes |
|---|---|---|
| `Id` | `Guid` | Primary key. |
| `ProjectId` | `Guid` | Foreign key → `Projects(Id)`, `ON DELETE CASCADE`. |
| `TemplateId` | `Guid` | Foreign key → `ReleaseNoteTemplates(Id)`, `ON DELETE RESTRICT` (deleting a template should fail if any binding still references it). |
| `Kind` | `TemplateKind` (enum) | `ReleaseNotes`, `Checklist`, `Custom`. Stored as `INTEGER`. |
| `PageTitleTemplate` | `string` (max 500) | Handlebars template for the Confluence page title. |
| `ParentPageIdOverride` | `string?` | Nullable. When null, the project's default Confluence parent page is used. |
| `LinkFromReleaseNotes` | `bool` | When true, this page is added to the primary release-notes page's related-pages section. Default false. |
| `SortOrder` | `int` | Determines preview and publish order. Default 0. |
| `CreatedAtUtc` | `DateTimeOffset` | Audit timestamp. |
| `UpdatedAtUtc` | `DateTimeOffset` | Audit timestamp. |

**Domain invariants** (enforced in application layer, not DB):

- Exactly one binding with `Kind = ReleaseNotes` per project must exist for that project to participate in releases.
- `PageTitleTemplate` is non-empty and ≤ 500 characters raw (rendered length is validated at preview time per FR-011).
- Custom variable keys (when referenced in templates) match `[a-zA-Z][a-zA-Z0-9_]*`.

**Indexes**:

- Unique: `(ProjectId, TemplateId, Kind)` — a project cannot bind the same template for the same kind twice.
- Non-unique: `(ProjectId, SortOrder)` — accelerates the wizard's "list bindings in order" query.

### `Project` (extended)

The existing `Project` entity gains two new columns and deprecates one.

| Property | Status | Notes |
|---|---|---|
| `VersionBumpStrategy` | **NEW** | Enum: `Patch=0`, `Minor=1`, `Major=2`. Stored as `INTEGER`. Default `Minor (1)`. |
| `CustomVariables` | **NEW** | Owned collection `IReadOnlyCollection<ProjectCustomVariable>`. Serialised to `CustomVariablesJson TEXT NULL`. |
| `DefaultReleaseNoteTemplateId` | **DEPRECATED** | Marked `[Obsolete]`. Read only by the seed migration and a legacy fallback in the Settings UI. Will be dropped in a follow-up migration. |

### `ProjectCustomVariable` (new value object)

```csharp
public sealed record ProjectCustomVariable(string Key, string Value);
```

Stored as JSON on `Projects.CustomVariablesJson` as `{ "<key>": "<value>", ... }`. Plain text — explicitly not for secrets (FR-021).

### `TemplateKind` (new enum)

```csharp
public enum TemplateKind
{
    ReleaseNotes = 0,
    Checklist    = 1,
    Custom       = 2,
}
```

### `VersionBumpStrategy` (new enum)

```csharp
public enum VersionBumpStrategy
{
    Patch = 0,
    Minor = 1,
    Major = 2,
}
```

---

## Schema diagram

```
┌──────────────────────────────┐
│ Projects (existing)          │
│                              │
│ Id                       PK  │◄────┐
│ Name                         │     │
│ ...                          │     │
│ DefaultReleaseNoteTemplateId │     │ FK
│   [Obsolete]                 │     │
│ VersionBumpStrategy   [NEW]  │     │ (cascade delete)
│ CustomVariablesJson   [NEW]  │     │
└──────────────────────────────┘     │
                                     │
                                     │
┌──────────────────────────────┐     │
│ ProjectTemplateBindings [NEW]│     │
│                              │     │
│ Id                       PK  │     │
│ ProjectId               FK ──┼─────┘
│ TemplateId              FK ──┼──┐
│ Kind                         │  │
│ PageTitleTemplate            │  │
│ ParentPageIdOverride         │  │
│ LinkFromReleaseNotes         │  │
│ SortOrder                    │  │
│ CreatedAtUtc                 │  │
│ UpdatedAtUtc                 │  │
└──────────────────────────────┘  │
                                  │
                                  │
┌──────────────────────────────┐  │
│ ReleaseNoteTemplates (exist) │  │
│                              │  │
│ Id                       PK  │◄─┘ (restrict delete)
│ Name                         │
│ BodyTemplate                 │
│ ...                          │
└──────────────────────────────┘
```

---

## Migrations

Four EF Core migrations, applied in order, all under `backend/src/RepoManager.Infrastructure/Persistence/Migrations/`:

### 1. `20260524_AddProjectTemplateBindings`

Creates the `ProjectTemplateBindings` table with FKs and indexes per the entity definition above.

### 2. `20260524_AddProjectVersionBumpStrategy`

Adds `VersionBumpStrategy INTEGER NOT NULL DEFAULT 1` to `Projects`.

### 3. `20260524_AddProjectCustomVariables`

Adds `CustomVariablesJson TEXT NULL` to `Projects`. EF's value converter handles serialisation of the owned collection.

### 4. `20260524_SeedDefaultReleaseNotesBindings` (data migration)

For every project where `DefaultReleaseNoteTemplateId IS NOT NULL`, insert one row into `ProjectTemplateBindings`:

```sql
INSERT INTO ProjectTemplateBindings (
    Id, ProjectId, TemplateId, Kind,
    PageTitleTemplate, ParentPageIdOverride,
    LinkFromReleaseNotes, SortOrder,
    CreatedAtUtc, UpdatedAtUtc
)
SELECT
    lower(hex(randomblob(16))),                    -- new Guid
    p.Id,
    p.DefaultReleaseNoteTemplateId,
    0,                                              -- ReleaseNotes
    '{{project.name}} {{version}} — Release Notes',
    NULL,
    0,
    0,
    datetime('now'),
    datetime('now')
FROM Projects p
WHERE p.DefaultReleaseNoteTemplateId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM ProjectTemplateBindings b
      WHERE b.ProjectId = p.Id AND b.Kind = 0
  );
```

The `NOT EXISTS` guard makes the migration safe to re-run.

**Down migration**: drop the inserted rows by joining back to `Projects.DefaultReleaseNoteTemplateId`. This is best-effort — the `[Obsolete]` column being kept for one release exists precisely to keep this rollback possible.

---

## EF Core configuration sketches

### `ProjectTemplateBindingConfiguration`

```csharp
public sealed class ProjectTemplateBindingConfiguration
    : IEntityTypeConfiguration<ProjectTemplateBinding>
{
    public void Configure(EntityTypeBuilder<ProjectTemplateBinding> b)
    {
        b.ToTable("ProjectTemplateBindings");
        b.HasKey(x => x.Id);

        b.Property(x => x.PageTitleTemplate).IsRequired().HasMaxLength(500);
        b.Property(x => x.Kind).HasConversion<int>();

        b.HasIndex(x => new { x.ProjectId, x.TemplateId, x.Kind }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.SortOrder });

        b.HasOne<Project>().WithMany(p => p.TemplateBindings)
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);

        b.HasOne<ReleaseNoteTemplate>().WithMany()
            .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### `ProjectConfiguration` (additions)

```csharp
b.Property(x => x.VersionBumpStrategy).HasConversion<int>();

b.Property<string?>("CustomVariablesJson");
b.Ignore(x => x.CustomVariables);  // wire up via OwnsMany or a custom converter
```

A `JsonStringDictionaryConverter` (custom value converter) translates between `IReadOnlyCollection<ProjectCustomVariable>` and `CustomVariablesJson`. Constraint: keep the converter pure — no domain logic in the conversion path.

---

## Backward-compatibility guarantees

- Reading any existing release: unchanged. The published Release record stores the Confluence URL it already stored.
- Editing an existing project that has a single seeded binding: unchanged behaviour. The Pages settings tab shows one row pre-populated.
- Deleting a project: cascade-deletes its bindings as expected; `Restrict` on `TemplateId` ensures we don't lose a template that another project might still bind.
- Removing `DefaultReleaseNoteTemplateId`: deferred to a follow-up migration. Until then, the column stays in the schema for clean rollback.
