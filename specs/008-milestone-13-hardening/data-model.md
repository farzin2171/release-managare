---

# Data Model: Milestone 13 — Security, Service Ownership & UX Hardening

**Branch**: `009-milestone-13-hardening`
**Date**: 2026-05-30

---

## Entity Changes

### `Repository` (modified)

**New field**:

| Column | Type | Nullable | Default | Constraint |
|--------|------|----------|---------|------------|
| `ServiceOwner` | `string` | ✓ | `null` | max 120 chars |

EF Core configuration:
```csharp
builder.Property(r => r.ServiceOwner)
       .HasMaxLength(120)
       .IsRequired(false);
```

No backfill required — existing rows default to `null`.

---

### `ReleaseNoteTemplate` (modified)

**New field**:

| Column | Type | Nullable | Default | Constraint |
|--------|------|----------|---------|------------|
| `IsSystem` | `bool` | ✗ | `false` | — |

EF Core configuration:
```csharp
builder.Property(t => t.IsSystem)
       .HasDefaultValue(false)
       .IsRequired();
```

---

## Migrations

### Migration 1: `AddColumn_Repositories_ServiceOwner`

```csharp
// Up
migrationBuilder.AddColumn<string>(
    name: "ServiceOwner",
    table: "Repositories",
    type: "TEXT",
    maxLength: 120,
    nullable: true,
    defaultValue: null);

// Down
migrationBuilder.DropColumn(name: "ServiceOwner", table: "Repositories");
```

---

### Migration 2: `AddColumn_Templates_IsSystem`

```csharp
// Up
migrationBuilder.AddColumn<bool>(
    name: "IsSystem",
    table: "ReleaseNoteTemplates",
    type: "INTEGER",
    nullable: false,
    defaultValue: false);

migrationBuilder.InsertData(
    table: "ReleaseNoteTemplates",
    columns: new[] { "Name", "Body", "IsSystem", "CreatedAt" },
    values: new object[] {
        "Release Summary (Default)",
        ReleaseSummaryTemplateBody.Default,  // const string in Infrastructure
        true,
        DateTime.UtcNow
    });

// Down
migrationBuilder.DeleteData(
    table: "ReleaseNoteTemplates",
    keyColumn: "Name",
    keyValue: "Release Summary (Default)");

migrationBuilder.DropColumn(name: "IsSystem", table: "ReleaseNoteTemplates");
```

The template body constant `ReleaseSummaryTemplateBody.Default` lives in `RepoManager.Infrastructure/Persistence/SeedData/ReleaseSummaryTemplateBody.cs`.

---

## DTO Changes

### `RepositoryDto` (modified)

Add field:
```csharp
string? ServiceOwner
```

### `UpdateRepositoryRequest` (modified)

Add field:
```csharp
string? ServiceOwner  // max 120 chars, validated in UpdateRepositoryRequestValidator
```

Validator addition:
```csharp
RuleFor(x => x.ServiceOwner)
    .MaximumLength(120)
    .When(x => x.ServiceOwner is not null);
```

### `ReleaseNoteTemplateDto` (modified)

Add field:
```csharp
bool IsSystem
```

### `CloneTemplateRequest` / `CloneTemplateResponse` (new)

```csharp
// No request body needed — clone source identified by route parameter {id}
// Response: same shape as ReleaseNoteTemplateDto (the new clone)
```

---

## New DTOs

### `RepoSummaryContext` (new — Application layer)

```csharp
record RepoSummaryContext(
    string Name,
    string ServiceOwner,    // Empty string ("") when Repository.ServiceOwner is null
    string PreviousVersion,
    string NextVersion,
    int    CommitCount,
    int    TicketCount
);
```

### `ReleaseRenderContext` (extended)

Add to existing context record:
```csharp
IReadOnlyList<RepoSummaryContext> Repositories
```

Mapping in `TemplateRenderingService.BuildContextAsync`:
```csharp
Repositories = releaseRepositories
    .Select(rr => new RepoSummaryContext(
        Name:            rr.Repository.Name,
        ServiceOwner:    rr.Repository.ServiceOwner ?? "",
        PreviousVersion: rr.PreviousVersion ?? "",
        NextVersion:     rr.NextVersion ?? "",
        CommitCount:     rr.CommitCount,
        TicketCount:     rr.TicketCount
    ))
    .ToList()
    .AsReadOnly()
```

---

## Clone Naming Logic

When `CloneAsync(templateId)` is called, the implementation determines the clone name as follows:

1. `baseName = $"{originalTemplate.Name} (copy)"`
2. Query all template names in the database that start with `baseName`.
3. If `baseName` is available → use it.
4. Otherwise, try `baseName + " 2"`, `baseName + " 3"`, … until a free slot is found (or a safety cap of 100 is reached, at which point a `ConflictException` is thrown).
5. The check-and-insert runs inside a database transaction to prevent races.

Note: The parenthetical suffix uses `(copy)`, `(copy 2)`, `(copy 3)` — not `(copy 1)` for the first occurrence.

---

## State Transition — `ReleaseNoteTemplate.IsSystem`

```
Seeded row:      IsSystem = true   (immutable — no PUT/DELETE allowed)
Clone of seeded: IsSystem = false  (fully editable, deletable)
Admin-created:   IsSystem = false  (fully editable, deletable)
```

There is no path from `IsSystem = false` to `IsSystem = true`. System templates are created only by seed migrations.

---

## Security Notes

- `RELEASE_MANAGER_SETUP_KEY` is **never** persisted to the database. It exists solely in the process environment.
- The refresh token value is stored in the `RefreshTokens` table (existing Milestone 1 schema). The httpOnly cookie change is a transport-layer concern only — the database schema is unchanged.
