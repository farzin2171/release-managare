# Data Model: Repository Release Management Platform

**Phase 1 output** — Entity schema, relationships, validation rules, and state transitions.

---

## Entity Overview

```
Users ─────────────────────────────────────────────────────────────────────┐
                                                                            │ CreatedBy
GitProviderConnections ──────────────────────────────────────────────────┐ │
         │                                                               │ │
         │ 1:N                                                           │ │
         ▼                                                               │ │
   Repositories ──────────────────────────────────────────────────────┐ │ │
         │ N:M (ProjectRepositories)                                   │ │ │
         │                                                             │ │ │
         ▼                                                             │ │ │
      Projects ◄──── ReleaseNoteTemplates (default template)          │ │ │
         │                                                             │ │ │
         │ 1:N                                                         │ │ │
         ▼                                                             │ │ │
      Releases ◄─────────────────── ReleaseRepositoryTags ────────────┘ │ │
         │                                                               │ │
         │ 1:1                                                           │ │
         ▼                                                               │ │
  ReleaseReconciliations                                                 │ │
                                                                         │ │
   Commits ──────────────────────────────────────────────────────────────┘ │
   Tickets (aggregation view)                                               │
                                                                            │
ConfluenceConnections                                                       │
JiraConnections ──── JiraReleases ──── JiraTickets                         │
                                                                            │
Releases ◄──────────────────────────────────────────────────────────────────┘
```

---

## Entities

### Users

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| Email | string(256) | UNIQUE, NOT NULL |
| PasswordHash | string | NOT NULL |
| Role | enum (Admin=0, Viewer=1) | NOT NULL |
| IsActive | bool | NOT NULL, DEFAULT true |
| CreatedAt | DateTimeOffset | NOT NULL |
| LastLoginAt | DateTimeOffset? | nullable |
| RefreshTokenHash | string? | nullable |
| RefreshTokenExpiresAt | DateTimeOffset? | nullable |

**Validation**:
- Email: valid email format, max 256 chars
- Role: must be Admin or Viewer

**Notes**: Passwords hashed with BCrypt (work factor 12). Refresh token stored as SHA-256 hash of the raw token.

---

### GitProviderConnections

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| ProviderType | enum (AzureDevOps=0) | NOT NULL |
| Name | string(200) | NOT NULL |
| OrganizationUrl | string(500) | NOT NULL |
| EncryptedPat | string | NOT NULL (IDataProtectionProvider) |
| IsActive | bool | NOT NULL, DEFAULT true |
| LastSyncedAt | DateTimeOffset? | nullable |
| LastTestStatus | string(50)? | nullable (Success / Failed / Untested) |

**Validation**:
- Name: required, max 200
- OrganizationUrl: valid URL, must end without trailing slash normalised on save
- EncryptedPat: required

---

### Repositories

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| GitProviderConnectionId | Guid | FK → GitProviderConnections |
| ExternalId | string(200) | NOT NULL |
| Name | string(300) | NOT NULL |
| DefaultBranch | string(200) | NOT NULL |
| WebUrl | string(500) | NOT NULL |
| AzureProjectName | string(200) | NOT NULL |
| IsTracked | bool | NOT NULL, DEFAULT false |
| LastSyncedAt | DateTimeOffset? | nullable |
| UNIQUE | (GitProviderConnectionId, ExternalId) | |

**Indexes**: `(GitProviderConnectionId, ExternalId)` UNIQUE, `IsTracked`

---

### Projects

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| Name | string(200) | NOT NULL |
| Description | string(1000)? | nullable |
| Color | string(7) | NOT NULL (hex #RRGGBB) |
| ReleaseNoteTemplateId | Guid? | FK → ReleaseNoteTemplates, nullable |
| ConfluenceSpaceKey | string(100)? | nullable |
| ConfluenceParentPageId | string(100)? | nullable |
| JiraConnectionId | Guid? | FK → JiraConnections, nullable |
| JiraProjectKeys | string | NOT NULL, JSON array (e.g. `["APPLY","CORE"]`) |
| FixVersionPattern | string(200)? | nullable (e.g. `"Apply {version}"`) |
| AutoCreateFixVersion | bool | NOT NULL, DEFAULT false |
| MatchSubtasksToParents | bool | NOT NULL, DEFAULT false |
| CreatedAt | DateTimeOffset | NOT NULL |
| UpdatedAt | DateTimeOffset | NOT NULL |

**Validation**:
- Name: required, max 200, unique
- Color: must match `^#[0-9A-Fa-f]{6}$`
- JiraProjectKeys: valid JSON array; each key matches `^[A-Z]{2,10}$`
- FixVersionPattern: must contain `{version}` if non-null

---

### ProjectRepositories (join table)

| Column | Type | Constraints |
|--------|------|-------------|
| ProjectId | Guid | FK → Projects |
| RepositoryId | Guid | FK → Repositories |
| IsPrimary | bool | NOT NULL, DEFAULT false |

**PK**: composite `(ProjectId, RepositoryId)`

**Constraint**: At most one row per ProjectId may have `IsPrimary = true`. Enforced in `ProjectService.AssignRepositoryAsync`.

---

### Commits

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| RepositoryId | Guid | FK → Repositories |
| Sha | string(40) | NOT NULL |
| ShortSha | string(8) | NOT NULL |
| Message | string | NOT NULL |
| AuthorName | string(200) | NOT NULL |
| AuthorEmail | string(256) | NOT NULL |
| CommittedAt | DateTimeOffset | NOT NULL |
| Type | string(50)? | nullable (parsed conventional type) |
| Scope | string(200)? | nullable |
| Description | string(500)? | nullable |
| IsBreaking | bool | NOT NULL, DEFAULT false |
| IsConventional | bool | NOT NULL, DEFAULT false |
| JiraTicketId | string(50)? | nullable (scope if matches Jira pattern) |
| UNIQUE | (RepositoryId, Sha) | |

**Indexes**: `(RepositoryId, Sha)` UNIQUE, `JiraTicketId`, `CommittedAt`

---

### Tickets (aggregation projection)

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| TicketId | string(50) | NOT NULL |
| RepositoryId | Guid | FK → Repositories |
| FromTag | string(200) | NOT NULL |
| ToTag | string(200) | NOT NULL |
| Title | string(500)? | nullable |
| PrimaryType | string(50)? | nullable |
| IsBreaking | bool | NOT NULL |
| CommitCount | int | NOT NULL |
| ContributorCount | int | NOT NULL |
| FirstCommittedAt | DateTimeOffset | NOT NULL |
| LastCommittedAt | DateTimeOffset | NOT NULL |

**Index**: `(RepositoryId, FromTag, ToTag, TicketId)` — queries always filter by this tuple.

**Lifecycle**: Rows are dropped and re-inserted for a given `(RepositoryId, FromTag, ToTag)` range on each `CommitSyncService.SyncAsync` call (idempotent projection).

---

### Releases

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| ProjectId | Guid | FK → Projects |
| Version | string(50) | NOT NULL |
| Status | enum (Draft=0, Published=1) | NOT NULL, DEFAULT Draft |
| GeneratedNotesMarkdown | string | NOT NULL |
| EditedNotesMarkdown | string? | nullable |
| ConfluencePageId | string(100)? | nullable |
| ConfluencePageUrl | string(500)? | nullable |
| CreatedByUserId | Guid | FK → Users |
| CreatedAt | DateTimeOffset | NOT NULL |
| PublishedAt | DateTimeOffset? | nullable |

**Validation**:
- Version: required, max 50
- Status transition: Draft → Published only (no revert). Enforced in `ReleaseService.PublishAsync`.

**State transitions**:
```
Draft ──[PublishAsync]──► Published (terminal)
```

Once Published: `EditedNotesMarkdown` is read-only; `ConfluencePageUrl` is set.

---

### ReleaseRepositoryTags

| Column | Type | Constraints |
|--------|------|-------------|
| ReleaseId | Guid | FK → Releases |
| RepositoryId | Guid | FK → Repositories |
| FromTag | string(200) | NOT NULL |
| ToTag | string(200) | NOT NULL |
| CommitCount | int | NOT NULL |

**PK**: composite `(ReleaseId, RepositoryId)`

---

### ReleaseNoteTemplates

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| Name | string(200) | NOT NULL, UNIQUE |
| ContentTemplate | string | NOT NULL (Handlebars) |
| IsDefault | bool | NOT NULL, DEFAULT false |

**Validation**:
- Name: required, max 200, unique
- ContentTemplate: required; validated that Handlebars can compile it without error

---

### ConfluenceConnections

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| BaseUrl | string(500) | NOT NULL |
| Username | string(256) | NOT NULL |
| EncryptedApiToken | string | NOT NULL |
| ChecklistTemplate | string? | nullable |
| IsActive | bool | NOT NULL, DEFAULT true |
| LastTestedAt | DateTimeOffset? | nullable |
| LastTestStatus | string(50)? | nullable |

**Note**: Only one ConfluenceConnection expected (single-tenant). `ConfluenceConnectionService` returns the single active record.

---

### JiraConnections

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| BaseUrl | string(500) | NOT NULL |
| Username | string(256) | NOT NULL |
| EncryptedApiToken | string | NOT NULL |
| IsActive | bool | NOT NULL, DEFAULT true |
| LastTestedAt | DateTimeOffset? | nullable |
| TestStatus | string(50)? | nullable |

---

### JiraReleases

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| JiraConnectionId | Guid | FK → JiraConnections |
| ProjectId | Guid | FK → Projects |
| JiraProjectKey | string(20) | NOT NULL |
| JiraVersionId | string(100) | NOT NULL |
| Name | string(200) | NOT NULL |
| Description | string(1000)? | nullable |
| IsReleased | bool | NOT NULL |
| ReleaseDate | DateOnly? | nullable |
| LastSyncedAt | DateTimeOffset | NOT NULL |
| UNIQUE | (JiraConnectionId, JiraVersionId) | |

---

### JiraTickets

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| JiraReleaseId | Guid | FK → JiraReleases |
| Key | string(50) | NOT NULL |
| Summary | string(500) | NOT NULL |
| Status | string(100) | NOT NULL |
| StatusCategory | enum (ToDo=0, InProgress=1, Done=2) | NOT NULL |
| IssueType | string(100) | NOT NULL |
| AssigneeName | string(200)? | nullable |
| AssigneeEmail | string(256)? | nullable |
| Priority | string(50)? | nullable |
| ParentKey | string(50)? | nullable (subtask parent) |
| LastSyncedAt | DateTimeOffset | NOT NULL |
| UNIQUE | (JiraReleaseId, Key) | |

---

### ReleaseReconciliations

| Column | Type | Constraints |
|--------|------|-------------|
| Id | Guid | PK |
| ReleaseId | Guid | FK → Releases, UNIQUE |
| JiraReleaseId | Guid | FK → JiraReleases |
| RunAt | DateTimeOffset | NOT NULL |
| MatchedCount | int | NOT NULL |
| JiraOnlyCount | int | NOT NULL |
| GitOnlyCount | int | NOT NULL |
| MatchRatePercent | decimal(5,2) | NOT NULL |
| Snapshot | string | NOT NULL (JSON blob of full ReconciliationResultDto) |

**Note**: One reconciliation per release (UNIQUE on ReleaseId). Re-running overwrites the existing row (upsert pattern).

---

## EF Core Entity Configuration Notes

- All entities use `Guid` PKs generated client-side (`Guid.NewGuid()`).
- `DateTimeOffset` columns stored as TEXT in SQLite (ISO 8601).
- JSON columns (`JiraProjectKeys`, `Snapshot`) stored as TEXT; accessed via `HasConversion` or raw JSON helper.
- Cascade delete: Repositories cascade-delete Commits and Tickets. Projects cascade-delete ProjectRepositories and Releases. JiraReleases cascade-delete JiraTickets. ReleaseReconciliations deleted when Release is deleted.
- No soft-delete pattern. Hard deletes only.

---

## Enumerations

```csharp
// Domain/Enums/
enum Role { Admin = 0, Viewer = 1 }
enum ProviderType { AzureDevOps = 0 }
enum ReleaseStatus { Draft = 0, Published = 1 }
enum ChangeType { Feat, Fix, Docs, Style, Refactor, Perf, Test, Build, Ci, Chore, Revert, Unknown }
enum JiraStatusCategory { ToDo = 0, InProgress = 1, Done = 2 }
```

---

## Key Validation Rules (FluentValidation)

One validator per write DTO in Application layer. Key rules:

| DTO | Key Rules |
|-----|-----------|
| `CreateUserDto` | Email valid + unique; password min 8 chars; Role in [Admin, Viewer] |
| `CreateGitConnectionDto` | Name required; OrganizationUrl valid URL; PAT required (min 20 chars) |
| `CreateProjectDto` | Name required, unique; Color matches `^#[0-9A-Fa-f]{6}$`; Description ≤ 1000 |
| `CreateReleaseDto` | Version matches semver pattern `^\d+\.\d+\.\d+$`; ProjectId must exist |
| `CreateTemplateDto` | Name required, unique; ContentTemplate compiles with HandlebarsDotNet |
| `UpdateConfluenceConnectionDto` | BaseUrl valid HTTPS URL; Username valid email; ApiToken required |
| `UpdateJiraConnectionDto` | Same pattern as Confluence |
